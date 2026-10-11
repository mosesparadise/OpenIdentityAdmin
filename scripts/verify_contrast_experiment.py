#!/usr/bin/env python3
"""Bounded synthetic WebKit experiment; kept separate from provider acceptance/coverage."""
import os
from pathlib import Path
import signal
import subprocess
import xml.etree.ElementTree as ET
from test_evidence import require_complete_trx
from test_selection import catalogue, require_execution_names

ROOT = Path(__file__).resolve().parents[1]
TEST = 'IdentityPlatform.Issuer.Tests.Features.Browser.ContrastExperimentTests.Synthetic_dark_error_page_preserves_contrast_across_scanner_and_page_lifecycles'
EXPRESSION = f'Provider=PostgreSql&FullyQualifiedName={TEST}'
SAMPLES = 40
MARKER = f'CONTRAST_EXPERIMENT_COMPLETE samples={SAMPLES} arms=5 contexts={SAMPLES * 5}'


def require_selection(all_tests, selected):
    expected_name = TEST + '(provider: "PostgreSql")'
    expected = {uid: test for uid, test in all_tests.items() if test == (expected_name, 'PostgreSql', True)}
    if len(expected) != 1 or selected != expected:
        raise ValueError('Focused contrast selection must contain exactly the designated synthetic WebKit case')


def require_completion(report):
    root = ET.parse(report).getroot()
    summaries, counters, results = root.findall('.//{*}ResultSummary'), root.findall('.//{*}Counters'), root.findall('.//{*}UnitTestResult')
    expected = {'total': 1, 'executed': 1, 'passed': 1, 'failed': 0}
    if len(summaries) != 1 or summaries[0].get('outcome') != 'Completed' or len(counters) != 1 or len(results) != 1:
        raise ValueError('Focused contrast run did not complete with exactly one result')
    if any(name not in counters[0].attrib or int(counters[0].get(name)) != value for name, value in expected.items()):
        raise ValueError('Focused contrast counters disagree with a complete pass')
    if any(int(value) for name, value in counters[0].attrib.items() if name not in expected):
        raise ValueError('Focused contrast report includes incomplete or skipped results')
    if results[0].get('outcome') != 'Passed' or root.findall('.//{*}RunInfo') or root.findall('.//{*}ErrorInfo'):
        raise ValueError('Focused contrast report contains unsuccessful results or run-level errors')
    outputs = results[0].findall('./{*}Output/{*}StdOut')
    lines = [line.strip() for node in outputs for line in (node.text or '').splitlines()]
    if lines.count(MARKER) != 1:
        raise ValueError('Focused contrast evidence lacks the complete bounded cohort')
    arms = ('Reference', 'NoInitialScan', 'PartialScan', 'PageLifecycle', 'NoInitialScreenshot')
    expected = {f'Contrast sample {sample}/{SAMPLES}: {arm} passed' for sample in range(1, SAMPLES + 1) for arm in arms}
    actual = [line for line in lines if line.startswith('Contrast sample ') and line.endswith(' passed')]
    if len(actual) != len(expected) or set(actual) != expected:
        raise ValueError('Focused contrast evidence drops, duplicates or substitutes an arm/sample')


def execute(command, environment, *, timeout=660, cleanup_timeout=10, capture=False):
    # The invocation limit matches ordinary browser verification. Never leave the browser tree running.
    process = subprocess.Popen(command, cwd=ROOT, env=environment, start_new_session=True,
                               stdout=subprocess.PIPE if capture else None, stderr=subprocess.STDOUT if capture else None, text=True)
    output = None
    try:
        if capture:
            output, _ = process.communicate(timeout=timeout)
            exit_code = process.returncode
        else:
            exit_code = process.wait(timeout=timeout)
    except subprocess.TimeoutExpired:
        raise RuntimeError('Focused contrast experiment exceeded its execution deadline') from None
    finally:
        # Clean surviving descendants even after the leader has already exited.
        for action in (signal.SIGTERM, signal.SIGKILL):
            try:
                os.killpg(process.pid, action)
            except ProcessLookupError:
                pass
            try:
                if capture:
                    process.communicate(timeout=cleanup_timeout)
                else:
                    process.wait(timeout=cleanup_timeout)
            except subprocess.TimeoutExpired:
                pass
        if process.stdout is not None:
            process.stdout.close()
        process.wait(timeout=cleanup_timeout)
    if exit_code:
        raise subprocess.CalledProcessError(exit_code, command)
    return output


def install_cancellation_handler():
    signal.signal(signal.SIGTERM, lambda signum, frame: (_ for _ in ()).throw(SystemExit(128 + signum)))


def main():
    install_cancellation_handler()
    results = ROOT / 'artifacts' / 'contrast-experiment'
    results.mkdir(parents=True, exist_ok=True)
    report = results / 'contrast.trx'
    report.unlink(missing_ok=True)
    project = ROOT / 'tests/aspire/IdentityPlatform.Issuer.Tests/IdentityPlatform.Issuer.Tests.csproj'
    environment = os.environ.copy()
    environment.update(IDENTITY_TEST_BROWSER='webkit', IDENTITY_CONTRAST_SAMPLES=str(SAMPLES),
                       TESTINGPLATFORM_TELEMETRY_OPTOUT='1', IDENTITY_TEST_ARTIFACTS=str(results))
    discovery = ['dotnet', 'run', '--project', str(project), '--no-build', '--no-restore', '--',
                 '--pre-enumerate-theories', 'on', '--list-tests', 'json']

    def discover(expression=None):
        invocation = discovery + (['--filter', expression] if expression else [])
        return catalogue(execute(invocation, environment, timeout=60, capture=True))

    all_tests, selected = discover(), discover(EXPRESSION)
    require_selection(all_tests, selected)
    command = ['dotnet', 'test', '--project', str(project), '--no-build', '--no-restore', '--no-progress',
               '--report-trx', '--report-trx-filename', report.name, '--results-directory', str(results),
               '--pre-enumerate-theories', 'on', '--filter', EXPRESSION]
    execute(command, environment)
    require_complete_trx(report, expected_count=1)
    require_execution_names(report, selected)
    require_completion(report)
    print(f'Focused WebKit cohort complete: {SAMPLES} samples per arm, 5 arms, {SAMPLES * 5} fresh sessions; no provider/coverage claim', flush=True)


if __name__ == '__main__':
    main()
