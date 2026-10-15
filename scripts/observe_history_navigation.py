#!/usr/bin/env python3
"""Run one exact synthetic Firefox refusal control with passive evidence, never acceptance credit."""
import os
import json
from pathlib import Path
import subprocess
import sys
import time
from test_selection import catalogue
from verify_contrast_experiment import execute, install_cancellation_handler
from history_navigation_receipt import require_receipt

ROOT = Path(__file__).resolve().parents[1]
METHOD = 'IdentityPlatform.Administrator.Tests.Features.Browser.HistoryObservationTests.Given_RealNoStorePost_When_BackCannotRestoreDocument_Then_NativeRefusalHasNoResponseCommitOrResubmission'
NAMESPACE = 'IdentityPlatform.Administrator.Tests.Features.Browser'
PROVIDER = 'PostgreSql'
TOTAL_SECONDS = 180
PARENT = '8578441753d847a49d3f2de0137152ad5fe88c2e'
REF = 'refs/heads/055-firefox-native-observation'


def require_launch(environment, event, head, parents):
    """Allow only the leased first push from the reviewed prerequisite head, never later pushes or reruns."""
    if (environment.get('GITHUB_RUN_ATTEMPT') != '1' or environment.get('GITHUB_EVENT_NAME') != 'push' or
            environment.get('GITHUB_REPOSITORY') != 'mosesparadise/OpenIdentityAdmin' or
            environment.get('GITHUB_REF') != REF or environment.get('GITHUB_SHA') != head or
            event.get('ref') != REF or event.get('before') != PARENT or event.get('after') != head or
            event.get('created') is not False or event.get('deleted') is not False or event.get('forced') is not False or
            len(head) != 40 or any(c not in '0123456789abcdef' for c in head) or parents != [PARENT]):
        raise RuntimeError('Not the single reviewed launch transition')


def check_launch():
    """Bind trusted event metadata to the checked-out commit and its sole parent before tooling or execution."""
    event = json.loads(Path(os.environ['GITHUB_EVENT_PATH']).read_text())
    head = subprocess.check_output(['git', 'rev-parse', 'HEAD'], cwd=ROOT, text=True).strip()
    parents = subprocess.check_output(['git', 'show', '-s', '--format=%P', 'HEAD'], cwd=ROOT, text=True).split()
    require_launch(os.environ, event, head, parents)


def remaining(end):
    """Reject exhaustion before process creation; never renew the absolute work budget."""
    value = end - time.monotonic()
    if value <= 0:
        raise RuntimeError('Observation work budget exhausted before process launch')
    return value


def main():
    """Require exact native selection and run once; retain a failed raw report without laundering it."""
    check_launch()
    install_cancellation_handler()
    end = time.monotonic() + TOTAL_SECONDS
    project = ROOT / 'tests/aspire/IdentityPlatform.Administrator.Tests/IdentityPlatform.Administrator.Tests.csproj'
    results = ROOT / 'artifacts/native-history-observation'
    results.mkdir(parents=True, exist_ok=True)
    report = results / 'observation.trx'
    if report.exists():
        raise RuntimeError('Observation report already exists; repeating this execution is forbidden')
    environment = os.environ.copy() | {'IDENTITY_TEST_BROWSER': 'firefox', 'DOTNET_ENVIRONMENT': 'Development',
                                      'IDENTITY_TEST_ARTIFACTS': str(results)}
    expression = f'Provider={PROVIDER}&FullyQualifiedName={METHOD}'
    discovery = ['dotnet', 'run', '--project', str(project), '--no-build', '--no-restore', '--',
                 '--pre-enumerate-theories', 'on', '--list-tests', 'json']
    all_tests = catalogue(execute(discovery, environment, timeout=min(60, remaining(end)), capture=True), project='Administrator')
    selected = catalogue(execute(discovery + ['--filter', expression], environment,
                                 timeout=min(60, remaining(end)), capture=True), project='Administrator')
    identity = (METHOD + f'(provider: "{PROVIDER}")', PROVIDER, True, NAMESPACE, 'Accounts')
    expected = {uid: value for uid, value in all_tests.items() if value == identity}
    if len(expected) != 1 or selected != expected:
        raise RuntimeError('Observation must select exactly its original native case and provider argument')
    command = ['dotnet', 'test', '--project', str(project), '--no-build', '--no-restore', '--no-progress',
               '--report-trx', '--report-trx-filename', report.name, '--results-directory', str(results),
               '--pre-enumerate-theories', 'on', '--filter', expression]
    native_error = None
    try:
        execute(command, environment, timeout=remaining(end))
    except subprocess.CalledProcessError as error:
        native_error = error
    try:
        receipt = require_receipt(report, identity[0], native_error.returncode if native_error else 0)
    except Exception:
        (results / 'receipt.json').write_text(json.dumps({'evidence_complete': False, 'acceptance_credit': False}) + '\n')
        if native_error:
            raise native_error
        raise
    (results / 'receipt.json').write_text(json.dumps(receipt, indent=2) + '\n')
    if native_error:
        raise native_error
    print('One diagnostic receipt reconciled; no database, ordinary acceptance, coverage or causal-closure credit.')


if __name__ == '__main__':
    if sys.argv[1:] == ['--check-launch']:
        check_launch()
    elif sys.argv[1:]:
        raise RuntimeError('Unknown observation argument')
    else:
        main()
