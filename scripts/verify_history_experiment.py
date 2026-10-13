#!/usr/bin/env python3
"""Run two fixed Firefox diagnostic arms without crediting them as ordinary acceptance or coverage."""
import argparse
import os
from pathlib import Path
import re
import time
import xml.etree.ElementTree as ET
from test_selection import catalogue, require_lifecycle_execution
from verify_contrast_experiment import execute, install_cancellation_handler

ROOT = Path(__file__).resolve().parents[1]
TEST = 'IdentityPlatform.Administrator.Tests.Features.Browser.HistoryObservationTests.Given_DisabledPageScripts_When_HistoryDocumentBecomesReady_Then_SecretsFailImmediatelyAndOnlySafeDocumentsPass'
NAMESPACE = 'IdentityPlatform.Administrator.Tests.Features.Browser'
MODE_VARIABLE = 'IDENTITY_HISTORY_EXPERIMENT'
PROVIDERS = ('PostgreSql', 'SqlServer')
ARMS = ('isolated', 'overlap')
DEADLINE_SECONDS = 660
POSITIVE_MICROSECONDS = 5_000_000
MAX_MICROSECONDS = DEADLINE_SECONDS * 1_000_000
WINDOW = re.compile(r'HISTORY_WINDOW unit=microseconds graph-start=(-?\d+) database-running=(-?\d+) graph-ready=(-?\d+) positive-start=(\d+) positive-end=(\d+)')
PROGRESS = re.compile(r'history-readiness elapsed-ms=(\d+) requested=1 released=True fulfilled=True loads=(\d+) safe=True cleanup-failed=False phase=Complete')


def require_selection(all_tests, selected, provider):
    """Require one exact native identity, provider and browser-story tuple, rejecting substituted UIDs."""
    if provider not in PROVIDERS:
        raise ValueError('Unknown history diagnostic provider')
    identity = (TEST + f'(provider: "{provider}")', provider, True, NAMESPACE, 'Accounts')
    expected = {uid: test for uid, test in all_tests.items() if test == identity}
    if len(expected) != 1 or selected != expected:
        raise ValueError('History diagnostic selection must contain exactly its designated native case')


def require_completion(report, provider, arm, selected):
    """Require successful fixed delivery/observation evidence and actual interval overlap for the selected arm."""
    if provider not in PROVIDERS or arm not in ARMS:
        raise ValueError('Unknown history diagnostic arm or provider')
    require_lifecycle_execution(report, selected)
    root = ET.parse(report).getroot()
    lines = [line.strip() for node in root.findall('.//{*}UnitTestResult/{*}Output/{*}StdOut')
             for line in (node.text or '').splitlines()]
    marker = f'HISTORY_EXPERIMENT mode={arm} provider={provider} completed=True overlap={arm == "overlap"}'
    metadata = [line for line in lines if line.startswith('HISTORY_')]
    windows = [WINDOW.fullmatch(line) for line in metadata if line.startswith('HISTORY_WINDOW')]
    progress = [line for line in lines if line.startswith('history-readiness')]
    if len(metadata) != 2 or metadata.count(marker) != 1 or len(windows) != 1 or windows[0] is None:
        raise ValueError('Missing, duplicate or substituted history experiment markers')
    if len(progress) != 1 or PROGRESS.fullmatch(progress[0]) is None:
        raise ValueError('History delivery or safe-observation evidence is incomplete')
    cleanup = [line for line in lines if line.startswith('history-graph')]
    if cleanup != (['history-graph cleanup-completed=True'] if arm == 'overlap' else []):
        raise ValueError('Graph cleanup evidence is missing, contradictory or belongs to the wrong arm')
    start, running, ready, positive, end = map(int, windows[0].groups())
    if not 0 <= positive < end <= MAX_MICROSECONDS or end - positive > POSITIVE_MICROSECONDS:
        raise ValueError('Missing or invalid positive observation interval')
    if arm == 'isolated' and (start, running, ready) != (-1, -1, -1):
        raise ValueError('Isolated diagnostic unexpectedly includes a graph')
    if arm == 'overlap' and not (0 <= start <= running <= positive < ready <= MAX_MICROSECONDS):
        raise ValueError('Actual startup overlap was not established')


def prepare_reports(results):
    """Remove prior fixed reports before discovery, so early failure cannot retain stale evidence."""
    for arm in ARMS:
        (results / f'{arm}.trx').unlink(missing_ok=True)
    record_outcomes(results, {arm: 'unattempted' for arm in ARMS})


def record_outcomes(results, outcomes):
    """Retain only fixed arm status labels, never raw subprocess errors or page details."""
    (results / 'outcomes.txt').write_text(''.join(f'{arm}={outcomes[arm]}\n' for arm in ARMS))


def run_arms(results, invoke, remaining):
    """Attempt both fixed arms once within the same deadline and preserve each independent outcome."""
    outcomes = {arm: 'unattempted' for arm in ARMS}
    for arm in ARMS:
        try:
            budget = remaining()
        except RuntimeError:
            record_outcomes(results, outcomes)
            break
        outcomes[arm] = 'started'
        record_outcomes(results, outcomes)
        try:
            invoke(arm, budget)
        except Exception:
            outcomes[arm] = 'failed'
        else:
            outcomes[arm] = 'passed'
        record_outcomes(results, outcomes)
    if any(value != 'passed' for value in outcomes.values()):
        raise RuntimeError('History comparison incomplete; inspect retained fixed-arm reports and outcomes')


def main():
    """Discover exactly, execute each arm once under one total budget, and retain separately reconciled artifacts."""
    parser = argparse.ArgumentParser()
    parser.add_argument('provider', choices=PROVIDERS)
    args = parser.parse_args()
    if MODE_VARIABLE in os.environ:
        parser.error('The history experiment runner owns its mode variable')
    install_cancellation_handler()
    deadline = time.monotonic() + DEADLINE_SECONDS
    project = ROOT / 'tests/aspire/IdentityPlatform.Administrator.Tests/IdentityPlatform.Administrator.Tests.csproj'
    results = ROOT / 'artifacts/history-experiment' / args.provider
    results.mkdir(parents=True, exist_ok=True)
    prepare_reports(results)
    environment = os.environ.copy()
    environment.update(IDENTITY_TEST_BROWSER='firefox', DOTNET_ENVIRONMENT='Development',
                       TESTINGPLATFORM_TELEMETRY_OPTOUT='1', IDENTITY_TEST_ARTIFACTS=str(results))
    expression = f'Provider={args.provider}&FullyQualifiedName={TEST}'
    discovery = ['dotnet', 'run', '--project', str(project), '--no-build', '--no-restore', '--',
                 '--pre-enumerate-theories', 'on', '--list-tests', 'json']

    def remaining():
        """Never reset the diagnostic deadline between discovery and the two fixed arms."""
        value = deadline - time.monotonic()
        if value <= 0:
            raise RuntimeError('History experiment exceeded its shared execution deadline')
        return value

    def discover(filtered):
        """Use native discovery independently of the subsequent test execution filter."""
        command = discovery + (['--filter', expression] if filtered else [])
        return catalogue(execute(command, environment, timeout=min(60, remaining()), capture=True), project='Administrator')

    all_tests, selected = discover(False), discover(True)
    require_selection(all_tests, selected, args.provider)
    def invoke(arm, budget):
        """Execute one arm at most once using the remaining shared budget and validate native report bindings."""
        report = results / f'{arm}.trx'
        arm_environment = environment | {MODE_VARIABLE: arm}
        command = ['dotnet', 'test', '--project', str(project), '--no-build', '--no-restore', '--no-progress',
                   '--report-trx', '--report-trx-filename', report.name, '--results-directory', str(results),
                   '--pre-enumerate-theories', 'on', '--filter', expression]
        execute(command, arm_environment, timeout=budget)
        require_completion(report, args.provider, arm, selected)

    run_arms(results, invoke, remaining)
    print('Verified two fixed Firefox history diagnostic arms; no ordinary provider/coverage credit', flush=True)


if __name__ == '__main__':
    main()
