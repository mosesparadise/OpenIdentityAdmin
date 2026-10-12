#!/usr/bin/env python3
"""Verify an isolated contract partition with exact discovery/execution identity."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from test_evidence import require_complete_trx
from test_selection import require_execution_names, require_lifecycle_execution

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'tests/contracts/IdentityPlatform.Api.Contracts.Tests/IdentityPlatform.Api.Contracts.Tests.csproj'
PACKAGE = ROOT / 'src/frontend/IdentityPlatform.Admin.Contracts'
MATRIX = json.loads((ROOT / 'eng/contract-test-matrix.json').read_text())


def run(command, **kwargs):
    return subprocess.run(command, cwd=ROOT, check=True, timeout=kwargs.pop('timeout', 180), **kwargs)


def catalogue(expression=None):
    command = ['dotnet', 'run', '--project', str(PROJECT), '--no-build', '--no-restore', '--', '--pre-enumerate-theories', 'on', '--list-tests', 'json']
    if expression:
        command += ['--filter', expression]
    raw = json.loads(run(command, capture_output=True, text=True).stdout)
    if raw.get('schemaVersion') != 1:
        raise ValueError('Unsupported contract discovery schema')
    tests = raw['tests']
    result = {test['uid']: (test['displayName'],) for test in tests}
    if not result or len(result) != len(tests) or len({test[0] for test in result.values()}) != len(result):
        raise ValueError('Missing or duplicate contract identity')
    return result


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('partition', choices=['core', *MATRIX['browsers']])
    args = parser.parse_args()
    label = 'contracts-' + args.partition
    output = ROOT / 'artifacts/tests' / label
    output.mkdir(parents=True, exist_ok=True)
    ordinary, browser = catalogue('Category!=Browser'), catalogue('Category=Browser')
    if ordinary.keys() & browser.keys() or ordinary | browser != catalogue():
        raise ValueError('Contract partitions omit/duplicate cases')
    if len(ordinary) != MATRIX['core'] or len(browser) != MATRIX['browser']:
        raise ValueError('Contract discovery changed without matrix review')
    selected = ordinary if args.partition == 'core' else browser
    expression = 'Category!=Browser' if args.partition == 'core' else 'Category=Browser'
    environment = os.environ.copy()
    environment['IDENTITY_CONTRACT_OUTPUT'] = str(ROOT / 'artifacts/contracts')
    if args.partition != 'core':
        environment['IDENTITY_TEST_BROWSER'] = args.partition
    report = output / 'Contracts.trx'
    report.unlink(missing_ok=True)
    run(['dotnet', 'test', '--project', str(PROJECT), '--no-build', '--no-restore', '--filter', expression,
         '--pre-enumerate-theories', 'on', '--report-trx', '--report-trx-filename', 'Contracts.trx',
         '--results-directory', str(output)], env=environment, timeout=240)
    require_complete_trx(report, expected_count=len(selected))
    require_execution_names(report, selected)
    require_lifecycle_execution(report, selected)  # Reuse strict MTP counter/definition/entry validation.
    if args.partition == 'core':
        run(['python3', 'scripts/export_api_contracts.py'])
        run(['python3', 'scripts/install_contract_tools.py'])
        run(['npm', 'run', 'check', '--prefix', str(PACKAGE)])
        files = sorted(str(p) for folder in ('generated', 'tests/generated') for p in (PACKAGE / folder).glob('*.json'))
        if len(files) != 4:
            raise ValueError('Expected two actual and two fixture documents')
        run(['node', str(PACKAGE / 'tooling/validate.mjs'), *files])
        run(['node', str(PACKAGE / 'tooling/baseline.mjs')])
        node_report = output / 'node.xml'
        with node_report.open('w') as stream:
            run(['node', '--test', '--test-reporter=junit', str(PACKAGE / 'tests/compatibility.test.mjs')], stdout=stream)
        root = ET.parse(node_report).getroot()
        cases = root.findall('.//testcase')
        if len(cases) != MATRIX['node'] or any(case.find(tag) is not None for case in cases for tag in ('failure', 'error', 'skipped')):
            raise ValueError('Node contract evidence is incomplete')
    print(f'Contract {args.partition}: exact identities passed; no skipped or missing cases.')


if __name__ == '__main__':
    main()
