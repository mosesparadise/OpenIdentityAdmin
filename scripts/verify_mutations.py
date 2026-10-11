#!/usr/bin/env python3
"""Verify representative security mutants in a disposable Linux checkout; never publish its builds."""
import argparse
from contextlib import contextmanager
import fcntl
import hashlib
import json
import os
from pathlib import Path
import re
import signal
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET

from test_selection import catalogue

ROOT = Path(__file__).resolve().parents[1]
PROJECT = 'tests/aspire/IdentityPlatform.Issuer.Tests/IdentityPlatform.Issuer.Tests.csproj'


class MutationError(ValueError):
    """A fixed, safe verification failure reason."""


def manifest(root):
    value = json.loads((root / 'eng/critical-mutations.json').read_text())
    mutations = value['mutations']
    if value['version'] != 1 or len(mutations) != 7 or len({item['id'] for item in mutations}) != 7:
        raise MutationError('Unexpected critical mutation inventory')
    for item in mutations:
        path = (root / item['path']).resolve()
        if not path.is_relative_to((root / 'src/backend/IdentityPlatform.AuthServer/Features').resolve()) or path.suffix != '.cs':
            raise MutationError('Mutation target is outside the permitted source boundary')
        if not item['before'] or item['before'] == item['after'] or path.read_text().count(item['before']) != 1:
            raise MutationError('Mutation source drift or ambiguous replacement')
    return mutations


@contextmanager
def changed_source(path, before, after):
    original = path.read_bytes()
    old, new = before.encode(), after.encode()
    if original.count(old) != 1 or old == new:
        raise MutationError('Mutation source drift or ambiguous replacement')
    try:
        path.write_bytes(original.replace(old, new))
        yield
    finally:
        path.write_bytes(original)
        if path.read_bytes() != original:
            raise MutationError('Mutation source restoration failed')


def validate_report(path, selected, exit_code, *, marker=None, method=None):
    """Require whole-run evidence; an expected assertion cannot mask a setup/cleanup error."""
    root = ET.parse(path).getroot()
    results = root.findall('.//{*}UnitTestResult')
    names = [item.attrib['testName'] for item in results]
    if len(names) != len(set(names)) or set(names) != set(selected):
        raise MutationError('Mutation execution identities do not match discovery')
    summaries = root.findall('.//{*}ResultSummary')
    counter_nodes = root.findall('.//{*}Counters')
    if len(summaries) != 1 or len(counter_nodes) != 1:
        raise MutationError('Mutation report requires exactly one summary and counters node')
    counters = counter_nodes[0]
    expected_failures = 1 if marker else 0
    expected = {'total': len(selected), 'executed': len(selected), 'passed': len(selected) - expected_failures, 'failed': expected_failures}
    for name, count in expected.items():
        if name not in counters.attrib or int(counters.attrib[name]) != count:
            raise MutationError('Mutation report counters do not match the required outcome')
    if any(int(value) for name, value in counters.attrib.items() if name not in expected):
        raise MutationError('Mutation report contains skipped, aborted or other incomplete results')
    summary = summaries[0]
    if summary.attrib.get('outcome') != ('Failed' if marker else 'Completed'):
        raise MutationError('Mutation run did not complete with its expected outcome')
    failures = [item for item in results if item.attrib.get('outcome') == 'Failed']
    if len(failures) != expected_failures or any(item.attrib.get('outcome') not in ('Passed', 'Failed') for item in results):
        raise MutationError('Mutation report contains unexpected result outcomes')
    if exit_code != (2 if marker else 0):
        raise MutationError('Mutation process exit code is not the expected native MTP result')
    # xUnit/MTP also emits one exact failure-name echo as a RunInfo. No other run error is permitted.
    infos = root.findall('.//{*}RunInfo')
    if len(infos) != len(failures) or summary.findall('.//{*}ErrorInfo'):
        raise MutationError('Mutation run contains fixture or run-level errors')
    for info, failure in zip(infos, failures):
        pattern = r'\[xUnit\.net [0-9:.]{1,15}\]\s+' + re.escape(failure.attrib['testName']) + r' \[FAIL\]'
        if info.attrib.get('outcome') != 'Error' or not re.fullmatch(pattern, info.findtext('{*}Text', '').strip()):
            raise MutationError('Mutation run contains an unexpected run-level diagnostic')
    if marker:
        error = failures[0].find('./{*}Output/{*}ErrorInfo')
        if len(root.findall('.//{*}ErrorInfo')) != 1 or error is None or len(error.findall('{*}Message')) != 1 or len(error.findall('{*}StackTrace')) != 1 or error.findtext('{*}Message', '').strip() != marker or method not in error.findtext('{*}StackTrace', ''):
            raise MutationError('Mutation failed outside its demonstrated behavioral bypass assertion')
    elif root.findall('.//{*}ErrorInfo'):
        raise MutationError('Passing mutation control contains errors')
    return {'total': len(results), 'passed': len(results) - len(failures), 'failed': len(failures), 'skipped': 0}


def run(command, timeout, environment, *, cleanup_timeout=10):
    # Raw output may contain an unexpected exception: never echo it or retain it as evidence.
    process = subprocess.Popen(command, cwd=ROOT, env=environment, stdout=subprocess.PIPE,
                               stderr=subprocess.STDOUT, text=True, start_new_session=True)
    try:
        output, _ = process.communicate(timeout=timeout)
    except BaseException:
        # The leader can already have exited while a descendant still owns its output pipe.
        for action in (signal.SIGTERM, signal.SIGKILL):
            try:
                os.killpg(process.pid, action)
            except ProcessLookupError:
                pass
            try:
                process.communicate(timeout=cleanup_timeout)
                # Still send SIGKILL next: a stubborn child may have redirected its pipes.
            except subprocess.TimeoutExpired:
                continue
        # An escaped descendant must not make exception cleanup wait forever on a pipe.
        if process.stdout is not None:
            process.stdout.close()
        if process.poll() is None:
            process.kill()
        process.wait(timeout=cleanup_timeout)
        raise
    return process.returncode, output


def build(environment):
    code, _ = run(['dotnet', 'build', PROJECT, '--no-restore', '--nologo'], 180, environment)
    if code:
        raise MutationError('Mutation build failed; this is not a killed mutant')


def discover(expression, environment):
    code, output = run(['dotnet', 'run', '--project', PROJECT, '--no-build', '--no-restore', '--',
                        '--pre-enumerate-theories', 'on', '--list-tests', 'json', '--filter', expression], 60, environment)
    if code:
        raise MutationError('Mutation discovery failed')
    return catalogue(output)


def execute(expression, selected, directory, environment, *, item=None):
    report = directory / 'result.trx'
    report.unlink(missing_ok=True)
    started = time.monotonic()
    code, _ = run(['dotnet', 'test', '--project', PROJECT, '--no-build', '--no-restore', '--no-progress',
                   '--pre-enumerate-theories', 'on', '--filter', expression, '--report-trx',
                   '--report-trx-filename', report.name, '--results-directory', str(directory)], 180, environment)
    counts = validate_report(report, selected, code, marker=item['marker'] if item else None,
                             method=item['method'] if item else None)
    return {**counts, 'seconds': round(time.monotonic() - started, 3), 'tests': sorted(selected)}


def require_clean_checkout(environment):
    code, status = run(['git', 'status', '--porcelain', '--untracked-files=all'], 30, environment)
    if code or status.strip():
        raise MutationError('Authoritative mutation evidence requires a clean disposable checkout, including tests and tooling')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('provider', choices=['PostgreSql', 'SqlServer'])
    parser.add_argument('--disposable-checkout', action='store_true', required=True,
                        help='Acknowledge that this isolated checkout and its builds will be discarded, never published.')
    args = parser.parse_args()
    # Let ordinary SIGTERM cancellation unwind restoration/child-process cleanup.
    signal.signal(signal.SIGTERM, lambda signum, frame: (_ for _ in ()).throw(SystemExit(128 + signum)))
    environment = os.environ.copy()
    environment.update(DOTNET_ENVIRONMENT='Development', TESTINGPLATFORM_TELEMETRY_OPTOUT='1')
    output = ROOT / 'artifacts/mutations' / args.provider
    output.mkdir(parents=True, exist_ok=True)
    evidence_path = output / 'evidence.json'
    evidence_path.unlink(missing_ok=True)
    private = ROOT / 'work'
    private.mkdir(exist_ok=True)
    with (private / 'critical-mutations.lock').open('w') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        items = manifest(ROOT)
        paths = sorted({item['path'] for item in items})
        require_clean_checkout(environment)
        code, commit = run(['git', 'rev-parse', 'HEAD'], 30, environment)
        if code:
            raise MutationError('Missing source commit identity')
        hashes = {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in paths}
        evidence = {'version': 1, 'commit': commit.strip(), 'provider': args.provider, 'source_sha256': hashes, 'mutations': []}
        with tempfile.TemporaryDirectory(prefix='critical-mutations-', dir=private) as temporary:
            directory = Path(temporary)
            build(environment)
            selections = {}
            for item in items:
                expression = f"Provider={args.provider}&CriticalDecision={item['id']}"
                selection = discover(expression, environment)
                if len(selection) != 1 or any(test[1] != args.provider or test[2] or not test[0].startswith(item['method'] + '(') for test in selection.values()):
                    raise MutationError('Mutation target identity or provider differs from the reviewed manifest')
                selections[item['id']] = (expression, selection)
            expression = f"Provider={args.provider}&(" + '|'.join('CriticalDecision=' + item['id'] for item in items) + ')'
            selected = {test[0] for _, values in selections.values() for test in values.values()}
            if len(selected) != len(items):
                raise MutationError('Mutation targets overlap')
            evidence['baseline'] = execute(expression, selected, directory, environment)
            print(f"{args.provider}: all {len(selected)} baseline cases passed", flush=True)
            for item in items:
                expression_one, selection = selections[item['id']]
                print(f"{args.provider}: testing {item['id']}", flush=True)
                with changed_source(ROOT / item['path'], item['before'], item['after']):
                    mutant_hash = hashlib.sha256((ROOT / item['path']).read_bytes()).hexdigest()
                    build(environment)
                    if discover(expression_one, environment) != selection:
                        raise MutationError('Mutation changed discovered test identities')
                    result = execute(expression_one, {test[0] for test in selection.values()}, directory, environment, item=item)
                evidence['mutations'].append({'id': item['id'], 'mutant_sha256': mutant_hash,
                                              'verdict': 'killed_by_demonstrated_bypass', **result})
                print(f"{args.provider}: {item['id']} killed at its demonstrated bypass", flush=True)
            if any(hashlib.sha256((ROOT / path).read_bytes()).hexdigest() != digest for path, digest in hashes.items()):
                raise MutationError('Source restoration hash mismatch')
            build(environment)
            restored = discover(expression, environment)
            if restored != {uid: test for _, values in selections.values() for uid, test in values.items()}:
                raise MutationError('Restored discovery differs from baseline')
            evidence['restored'] = execute(expression, selected, directory, environment)
            evidence['source_restored'] = True
        evidence_path.write_text(json.dumps(evidence, indent=2) + '\n')
        print(f"{args.provider}: seven behavioral kills; baseline/restored controls pass; source hashes restored", flush=True)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, RuntimeError, OSError, subprocess.TimeoutExpired, ET.ParseError) as error:
        # Fixed type only: raw exceptions/reports can contain synthetic credentials.
        reason = str(error) if isinstance(error, MutationError) else type(error).__name__
        print(f'Mutation verification error: {reason}; no acceptance evidence produced.', flush=True)
        raise SystemExit(1) from None
