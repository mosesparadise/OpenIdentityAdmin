#!/usr/bin/env python3
"""Verify representative security mutants in a disposable Linux checkout; never publish its builds."""
import argparse
from contextlib import contextmanager
from contextvars import ContextVar
import fcntl
import hashlib
import json
import os
import re
from pathlib import Path
import signal
import subprocess
import tempfile
import time
import xml.etree.ElementTree as ET

from test_selection import catalogue
from administrator_inventory import load_inventory, require_native_inventory

ROOT = Path(__file__).resolve().parents[1]
PROJECT = 'tests/aspire/IdentityPlatform.Issuer.Tests/IdentityPlatform.Issuer.Tests.csproj'
FAILURE_DIAGNOSTIC = "Exit code indicates failure: '2'. Please refer to https://aka.ms/testingplatform/exitcodes for more information."

MAXIMUM_MUTATION_ORDINAL = 8
DIAGNOSTIC_PREFIX = 'mutation-diagnostic '
PHASE = ContextVar('mutation_phase', default=('control', 'command', 0))
ROLES = frozenset(('control', 'baseline', 'mutant', 'restored'))
OPERATIONS = frozenset(('command', 'checkout', 'commit', 'build', 'discovery', 'execution'))
RECEIPT = ContextVar('mutation_receipt', default=None)
REPORT_LIMIT_BYTES = 2 * 1024 * 1024
COUNTER_LIMIT = 100000
COUNTER_NAMES = ('total', 'executed', 'passed', 'failed', 'error', 'timeout', 'aborted', 'inconclusive',
                 'passedButRunAborted', 'notRunnable', 'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending')
ASSERTION_CLASSES = ('TrueException', 'FalseException', 'EqualException', 'NotSameException', 'EmptyException', 'SingleException')
EXCEPTION_CLASSES = ('OperationCanceledException', 'TaskCanceledException', 'TimeoutException', 'SqlException', 'PostgresException',
                     'InvalidOperationException', 'FactorUnavailableException', 'GovernanceUnavailableException',
                     'HttpRequestException', 'DockerApiException', 'RpcException', 'IOException')
SETUP_STAGES = frozenset(('HostBuilder', 'HostBuild', 'HostStart', 'AuthReadiness', 'HostReady', 'HostCleanup',
                         'LeaseOpen', 'LeaseCreate', 'LeaseMigrate', 'LeaseProvision', 'LeaseReady', 'LeaseCleanup',
                         'JourneyLease', 'JourneyInitialize', 'JourneyServices', 'JourneyReady',
                         'EnrollmentStage', 'EnrollmentVerify', 'EnrollmentReady', 'WitnessCreate',
                         'WitnessSecurityPreparation', 'RequestPreparation', 'CodePreparation',
                         'AuthHealthy', 'LifecycleCaptured', 'ConnectionReady'))
HARNESS_FAILURES = {'Bootstrap harness startup failed.': 'harness-startup-failed',
                    'Bootstrap database setup failed.': 'harness-database-failed',
                    'Bootstrap host cleanup failed.': 'harness-cleanup-failed'}
HARNESS_PREFIXES = ('', 'System.InvalidOperationException: ', 'System.InvalidOperationException : ')
WITNESS_STAGES = frozenset(('Setup', 'Prepared', 'GateWait', 'GateArrived', 'OrderWait', 'WriterFirst', 'ObserverFirst',
                          'WriterSecurity', 'OrderComplete', 'CommitResult', 'WriterJoined', 'Drain', 'Security', 'Authority', 'Usable', 'Oracle'))
PRIVILEGE_ALTER = frozenset(('Succeeded', 'Denied', 'ProviderError', 'Cancelled', 'Unknown'))
PRIVILEGE_PROBE = frozenset(('Absent', 'Present', 'InvalidGovernance', 'Failed', 'Cancelled', 'NotObserved', 'Unknown'))


def report_categories(root):
    """Classify exact library type tokens without retaining any report-supplied strings."""
    text = '\n'.join(node.text or '' for tag in ('Message', 'StackTrace') for node in root.findall('.//{*}' + tag))
    allowed = ASSERTION_CLASSES + EXCEPTION_CLASSES
    classes = [name for name in allowed if re.search(r'(?<![A-Za-z0-9_])' + name + r'(?![A-Za-z0-9_])', text)]
    for name in ASSERTION_CLASSES:
        prefix = 'Assert.' + name.removesuffix('Exception') + '() Failure'
        if any((node.text or '').startswith(prefix) for node in root.findall('.//{*}Message')) and name not in classes:
            classes.append(name)
    for message, category in HARNESS_FAILURES.items():
        if any((node.text or '').strip() == prefix + message
               for prefix in HARNESS_PREFIXES for node in root.findall('.//{*}Message')):
            classes.append(category)
    return classes or ['unknown']


def report_checkpoints(root):
    """Keep only closed diagnostic lines; their presence is never an acceptance oracle."""
    checkpoints = []
    for node in root.findall('.//{*}StdOut'):
        for line in (node.text or '').splitlines():
            prefix, _, value = line.strip().partition(' ')
            if prefix == 'governance-witness-stage' and value in WITNESS_STAGES:
                checkpoints.append(value)
            if prefix == 'governance-setup-stage' and value in SETUP_STAGES:
                checkpoints.append({'setup': value})
            if prefix == 'governance-privilege':
                fields = value.split(' ')
                if len(fields) == 3 and fields[0] in PRIVILEGE_ALTER and fields[1] in PRIVILEGE_PROBE and re.fullmatch(r'-?[0-9]{1,6}|none', fields[2]):
                    checkpoints.append({'alter': fields[0], 'probe': fields[1], 'provider_code': None if fields[2] == 'none' else int(fields[2])})
    privilege = [index for index, item in enumerate(checkpoints) if isinstance(item, dict) and 'alter' in item]
    retained = set(privilege[-1:])
    bypass = next((index for index in privilege if checkpoints[index]['alter'] == 'Succeeded' or
                   checkpoints[index]['probe'] == 'Present'), None)
    if bypass is not None:
        retained.add(bypass)
    privilege_indices = set(privilege)
    ordinary = [index for index in range(len(checkpoints)) if index not in privilege_indices]
    retained.update(ordinary[-(32 - len(retained)):])
    return [checkpoints[index] for index in sorted(retained)]


def report_observation(path, item):
    """Read at most two MiB and project untrusted XML into a finite, bounded schema."""
    try:
        with path.open('rb') as source:
            raw = source.read(REPORT_LIMIT_BYTES + 1)
        if len(raw) > REPORT_LIMIT_BYTES or b'\x00' in raw or b'<!DOCTYPE' in raw.upper() or b'<!ENTITY' in raw.upper():
            return {'report': 'rejected'}
        root = ET.fromstring(raw)
    except FileNotFoundError:
        return {'report': 'missing'}
    except (OSError, ET.ParseError, ValueError):
        return {'report': 'unreadable'}
    counters = root.findall('.//{*}Counters')
    counts = {}
    if len(counters) == 1:
        for name in COUNTER_NAMES:
            value = counters[0].attrib.get(name, '')
            counts[name] = int(value) if re.fullmatch(r'[0-9]{1,6}', value) and int(value) <= COUNTER_LIMIT else None
    errors = root.findall('.//{*}ErrorInfo')
    marker = item['marker'] if item else None
    method = item['method'] if item else None
    return {'report': 'parsed', 'counters': counts, 'error_nodes': min(len(errors), COUNTER_LIMIT),
            'summary_errors': min(len(root.findall('.//{*}ResultSummary//{*}ErrorInfo')), COUNTER_LIMIT),
            'marker_match': bool(marker and any(e.findtext('{*}Message', '').strip() == marker for e in errors)),
            'stack_match': bool(method and any(method + '(' in e.findtext('{*}StackTrace', '') for e in errors)),
            'categories': report_categories(root), 'checkpoints': report_checkpoints(root)}


def retain_failure(report, item, error):
    """Best-effort local receipt never replaces a primary error or creates acceptance evidence."""
    context = RECEIPT.get()
    if context is None:
        return
    try:
        path, source, run_id = context
        role, operation, ordinal = PHASE.get()
        value = {'diagnostic_only': True, 'source_commit': source if re.fullmatch(r'[0-9a-f]{40}', source) else None,
                 'run_id': run_id if re.fullmatch(r'[0-9]{1,20}', run_id) else None,
                 'role': role, 'operation': operation, 'ordinal': ordinal, 'failure': failure_category(error),
                 **report_observation(report, item)}
        path.write_text(json.dumps(value, sort_keys=True) + '\n')
    except BaseException:
        # A second cancellation or failed local sink must not replace the original exception.
        pass


def perform(role, operation, action, *arguments, ordinal=0, **keywords):
    """Bind closed phase labels without retaining a command, filter or manifest identifier."""
    if role not in ROLES or operation not in OPERATIONS or type(ordinal) is not int or not 0 <= ordinal <= MAXIMUM_MUTATION_ORDINAL:
        raise MutationError('Invalid mutation diagnostic phase')
    token = PHASE.set((role, operation, ordinal))
    try:
        return action(*arguments, **keywords)
    finally:
        PHASE.reset(token)


def failure_category(error):
    """Classify only the exception type; never stringify commands, output or exception payloads."""
    if isinstance(error, subprocess.TimeoutExpired):
        return 'timeout'
    return 'cancelled' if isinstance(error, (KeyboardInterrupt, SystemExit)) else 'error'


def diagnostic(event, outcome, started, timeout, cleanup_timeout):
    """Emit fixed-schema observations; elapsed time is receipt time and may include cleanup."""
    try:
        role, operation, ordinal = PHASE.get()
        print(DIAGNOSTIC_PREFIX + json.dumps({
            'role': role, 'operation': operation, 'ordinal': ordinal, 'event': event, 'outcome': outcome,
            'budget_seconds': timeout, 'cleanup_budget_seconds': cleanup_timeout,
            'elapsed_ms': int((time.monotonic() - started) * 1000)
        }, sort_keys=True), flush=True)
    except Exception:
        # Best-effort observations cannot change command failure or mandatory teardown.
        pass



class MutationError(ValueError):
    """A fixed, safe verification failure reason."""


def manifest(root, suite='issuer'):
    suites = {
        'issuer': (7, 'eng/critical-mutations.json', 'src/backend/IdentityPlatform.AuthServer/Features'),
        'administrator': (8, 'eng/administrator-mutations.json', 'src/persistence/IdentityPlatform.Infrastructure/Features/Identity'),
        'administrator-protocol': (3, 'eng/administrator-protocol-mutations.json', None),
        'applications': (2, 'eng/application-mutations.json', None),
        'browser-session': (8, 'eng/browser-session-mutations.json', None),
        'governance': (6, 'eng/governance-mutations.json', 'src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Governance')
    }
    if suite not in suites:
        raise MutationError('Unknown mutation suite')
    expected, filename, boundary = suites[suite]
    value = json.loads((root / filename).read_text())
    mutations = value['mutations']
    if value['version'] != 1 or len(mutations) != expected or len({item['id'] for item in mutations}) != expected:
        raise MutationError('Unexpected critical mutation inventory')
    for item in mutations:
        path = (root / item['path']).resolve()
        witness_lock = suite == 'governance' and path == (root / 'src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Ceremonies/CeremonyLocks.cs').resolve()
        protocol_paths = {
            'src/persistence/IdentityPlatform.Infrastructure/Features/Identity/ProtocolAdmission/ProtocolAdmissionProof.cs',
            'src/persistence/IdentityPlatform.Infrastructure/Features/ProtocolStorage/Administrator/AdministratorAssertionLedger.cs',
            'src/backend/IdentityPlatform.AuthServer/Features/Protocol/Administrator/AdministratorPrincipal.cs'
        }
        browser_paths = {
            'src/backend/IdentityPlatform.Admin.Bff/Features/BrowserSessions/BrowserAntiforgeryData.cs',
            'src/backend/IdentityPlatform.Admin.Bff/Features/BrowserSessions/BrowserLogoutCookie.cs',
            'src/backend/IdentityPlatform.Admin.Bff/Features/BrowserSessions/BrowserSessionIdentity.cs',
            'src/backend/IdentityPlatform.AuthServer/Features/Protocol/Administrator/IssuerLogoutFlow.cs',
            'src/persistence/IdentityPlatform.Infrastructure/Features/Sessions/Browser/BrowserLogoutService.cs',
            'src/persistence/IdentityPlatform.Infrastructure/Features/Sessions/Browser/BrowserSessionPolicy.cs',
        }
        if suite in ('administrator-protocol', 'browser-session', 'applications'):
            allowed = ({'src/core/IdentityPlatform.Core/Features/Applications/ApplicationSummary.cs',
                        'src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Ceremonies/CeremonyTransaction.cs'}
                       if suite == 'applications' else protocol_paths if suite == 'administrator-protocol' else browser_paths)
            permitted = path in {(root / value).resolve() for value in allowed}
        else:
            permitted = path.is_relative_to((root / boundary).resolve()) or witness_lock
        if not permitted or path.suffix != '.cs':
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
    # Native MTP emits its fixed exit-code diagnostic, not the older xUnit failure-name echo.
    infos = root.findall('.//{*}RunInfo')
    if len(infos) != len(failures) or summary.findall('.//{*}ErrorInfo'):
        raise MutationError('Mutation run contains fixture or run-level errors')
    for info in infos:
        if info.attrib.get('outcome') != 'Error' or info.findtext('{*}Text', '').strip() != FAILURE_DIAGNOSTIC:
            raise MutationError('Mutation run contains an unexpected run-level diagnostic')
    if marker:
        error = failures[0].find('./{*}Output/{*}ErrorInfo')
        if len(root.findall('.//{*}ErrorInfo')) != 1 or error is None or len(error.findall('{*}Message')) != 1 or len(error.findall('{*}StackTrace')) != 1 or error.findtext('{*}Message', '').strip() != marker or method + '(' not in error.findtext('{*}StackTrace', ''):
            raise MutationError('Mutation failed outside its demonstrated behavioral bypass assertion')
    elif root.findall('.//{*}ErrorInfo'):
        raise MutationError('Passing mutation control contains errors')
    return {'total': len(results), 'passed': len(results) - len(failures), 'failed': len(failures), 'skipped': 0}


def cleanup_process(process, cleanup_timeout):
    """Preserve TERM/KILL, pipe closure and bounded leader wait; return is not descendant proof."""
    for action in (signal.SIGTERM, signal.SIGKILL):
        try:
            os.killpg(process.pid, action)
        except ProcessLookupError:
            pass
        try:
            process.communicate(timeout=cleanup_timeout)
        except subprocess.TimeoutExpired:
            continue
    if process.stdout is not None:
        process.stdout.close()
    if process.poll() is None:
        process.kill()
    process.wait(timeout=cleanup_timeout)


def run(command, timeout, environment, *, cleanup_timeout=10):
    """Supervise one command without retries; diagnostics grant no report or mutation acceptance."""
    started = time.monotonic()
    diagnostic('started', 'pending', started, timeout, cleanup_timeout)
    try:
        process = subprocess.Popen(command, cwd=ROOT, env=environment, stdout=subprocess.PIPE,
                                   stderr=subprocess.STDOUT, text=True, start_new_session=True)
    except BaseException as error:
        diagnostic('launch-failed', failure_category(error), started, timeout, cleanup_timeout)
        raise
    try:
        output, _ = process.communicate(timeout=timeout)
    except BaseException as error:
        try:
            diagnostic('command-failed', failure_category(error), started, timeout, cleanup_timeout)
        finally:
            # Even cancellation while reporting must enter the original mandatory teardown.
            try:
                cleanup_process(process, cleanup_timeout)
            except BaseException as cleanup_error:
                diagnostic('cleanup-incomplete', failure_category(cleanup_error), started, timeout, cleanup_timeout)
                raise
            diagnostic('cleanup-returned', 'returned', started, timeout, cleanup_timeout)
        raise
    outcome = 'zero-exit' if process.returncode == 0 else 'test-failure-exit' if process.returncode == 2 else 'other-exit'
    diagnostic('returned', outcome, started, timeout, cleanup_timeout)
    return process.returncode, output


def build(environment):
    code, _ = run(['dotnet', 'build', PROJECT, '--no-restore', '--nologo', '-m:1', '-nr:false'], 180, environment)
    if code:
        raise MutationError('Mutation build failed; this is not a killed mutant')


def discover(expression, environment):
    code, output = run(['dotnet', 'run', '--project', PROJECT, '--no-build', '--no-restore', '--',
                        '--pre-enumerate-theories', 'on', '--list-tests', 'json', '--filter', expression], 60, environment)
    if code:
        raise MutationError('Mutation discovery failed')
    if 'Administrator' in PROJECT:
        require_native_inventory(output, load_inventory(ROOT / 'eng/administrator-test-matrix.json'))
    return catalogue(output, project='Administrator' if 'Administrator' in PROJECT else 'Issuer')


def execute(expression, selected, directory, environment, *, item=None):
    report = directory / 'result.trx'
    report.unlink(missing_ok=True)
    started = time.monotonic()
    try:
        code, _ = run(['dotnet', 'test', '--project', PROJECT, '--no-build', '--no-restore', '--no-progress',
                       '--pre-enumerate-theories', 'on', '--filter', expression, '--report-trx',
                       '--report-trx-filename', report.name, '--results-directory', str(directory)], 180, environment)
        counts = validate_report(report, selected, code, marker=item['marker'] if item else None,
                                 method=item['method'] if item else None)
    except BaseException as error:
        retain_failure(report, item, error)
        raise
    return {**counts, 'seconds': round(time.monotonic() - started, 3), 'tests': sorted(selected)}


def require_clean_checkout(environment):
    code, status = run(['git', 'status', '--porcelain', '--untracked-files=all'], 30, environment)
    if code or status.strip():
        raise MutationError('Authoritative mutation evidence requires a clean disposable checkout, including tests and tooling')


def main():
    global PROJECT
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('provider', choices=['PostgreSql', 'SqlServer'])
    parser.add_argument('--suite', choices=['issuer', 'administrator', 'governance', 'administrator-protocol', 'browser-session', 'applications'], default='issuer')
    parser.add_argument('--disposable-checkout', action='store_true', required=True,
                        help='Acknowledge that this isolated checkout and its builds will be discarded, never published.')
    args = parser.parse_args()
    if args.suite in ('administrator', 'governance', 'administrator-protocol', 'browser-session', 'applications'):
        PROJECT = 'tests/aspire/IdentityPlatform.Administrator.Tests/IdentityPlatform.Administrator.Tests.csproj'
    # Let ordinary SIGTERM cancellation unwind restoration/child-process cleanup.
    signal.signal(signal.SIGTERM, lambda signum, frame: (_ for _ in ()).throw(SystemExit(128 + signum)))
    environment = os.environ.copy()
    environment.update(DOTNET_ENVIRONMENT='Development', TESTINGPLATFORM_TELEMETRY_OPTOUT='1')
    output = ROOT / 'artifacts/mutations' / (args.provider if args.suite == 'issuer' else args.suite + '-' + args.provider)
    output.mkdir(parents=True, exist_ok=True)
    evidence_path = output / 'evidence.json'
    evidence_path.unlink(missing_ok=True)
    private = ROOT / 'work'
    private.mkdir(exist_ok=True)
    with (private / 'critical-mutations.lock').open('w') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        items = manifest(ROOT, args.suite)
        paths = sorted({item['path'] for item in items})
        perform('control', 'checkout', require_clean_checkout, environment)
        code, commit = perform('control', 'commit', run, ['git', 'rev-parse', 'HEAD'], 30, environment)
        if code:
            raise MutationError('Missing source commit identity')
        receipt_path = output / 'failure-diagnostic.json'
        receipt_path.unlink(missing_ok=True)
        RECEIPT.set((receipt_path, commit.strip(), environment.get('GITHUB_RUN_ID', '')))
        hashes = {path: hashlib.sha256((ROOT / path).read_bytes()).hexdigest() for path in paths}
        evidence = {'version': 1, 'suite': args.suite, 'commit': commit.strip(), 'provider': args.provider, 'source_sha256': hashes, 'mutations': []}
        with tempfile.TemporaryDirectory(prefix='critical-mutations-', dir=private) as temporary:
            directory = Path(temporary)
            perform('baseline', 'build', build, environment)
            selections = {}
            for ordinal, item in enumerate(items, start=1):
                expression = f"Provider={args.provider}&CriticalDecision={item['id']}"
                selection = perform('baseline', 'discovery', discover, expression, environment, ordinal=ordinal)
                if len(selection) != 1 or any(test[1] != args.provider or test[2] or not test[0].startswith(item['method'] + '(') for test in selection.values()):
                    raise MutationError('Mutation target identity or provider differs from the reviewed manifest')
                selections[item['id']] = (expression, selection)
            expression = f"Provider={args.provider}&(" + '|'.join('CriticalDecision=' + item['id'] for item in items) + ')'
            selected = {test[0] for _, values in selections.values() for test in values.values()}
            if len(selected) != len(items):
                raise MutationError('Mutation targets overlap')
            evidence['baseline'] = perform('baseline', 'execution', execute, expression, selected, directory, environment)
            print(f"{args.provider}: all {len(selected)} baseline cases passed", flush=True)
            for ordinal, item in enumerate(items, start=1):
                expression_one, selection = selections[item['id']]
                print(f"{args.provider}: testing {item['id']}", flush=True)
                with changed_source(ROOT / item['path'], item['before'], item['after']):
                    mutant_hash = hashlib.sha256((ROOT / item['path']).read_bytes()).hexdigest()
                    perform('mutant', 'build', build, environment, ordinal=ordinal)
                    if perform('mutant', 'discovery', discover, expression_one, environment, ordinal=ordinal) != selection:
                        raise MutationError('Mutation changed discovered test identities')
                    result = perform('mutant', 'execution', execute, expression_one,
                                     {test[0] for test in selection.values()}, directory, environment, item=item, ordinal=ordinal)
                evidence['mutations'].append({'id': item['id'], 'mutant_sha256': mutant_hash,
                                              'verdict': 'killed_by_demonstrated_bypass', **result})
                print(f"{args.provider}: {item['id']} killed at its demonstrated bypass", flush=True)
            if any(hashlib.sha256((ROOT / path).read_bytes()).hexdigest() != digest for path, digest in hashes.items()):
                raise MutationError('Source restoration hash mismatch')
            perform('restored', 'build', build, environment)
            restored = perform('restored', 'discovery', discover, expression, environment)
            if restored != {uid: test for _, values in selections.values() for uid, test in values.items()}:
                raise MutationError('Restored discovery differs from baseline')
            evidence['restored'] = perform('restored', 'execution', execute, expression, selected, directory, environment)
            evidence['source_restored'] = True
        evidence_path.write_text(json.dumps(evidence, indent=2) + '\n')
        print(f"{args.provider}: {len(items)} behavioral kills; baseline/restored controls pass; source hashes restored", flush=True)


if __name__ == '__main__':
    try:
        main()
    except (ValueError, RuntimeError, OSError, subprocess.TimeoutExpired, ET.ParseError) as error:
        # Fixed type only: raw exceptions/reports can contain synthetic credentials.
        reason = str(error) if isinstance(error, MutationError) else type(error).__name__
        print(f'Mutation verification error: {reason}; no acceptance evidence produced.', flush=True)
        raise SystemExit(1) from None
