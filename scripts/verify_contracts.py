#!/usr/bin/env python3
"""Verify an isolated contract partition with exact discovery/execution identity."""
import argparse
import json
import os
from pathlib import Path
import subprocess
import xml.etree.ElementTree as ET
from contract_evidence import catalogue as read_catalogue, require_node_report
from test_evidence import require_complete_trx
from verify_contrast_experiment import execute, install_cancellation_handler
from test_selection import require_execution_names, require_lifecycle_execution

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'tests/contracts/IdentityPlatform.Api.Contracts.Tests/IdentityPlatform.Api.Contracts.Tests.csproj'
PACKAGE = ROOT / 'src/frontend/IdentityPlatform.Admin.Contracts'
MATRIX = json.loads((ROOT / 'eng/contract-test-matrix.json').read_text())


def run(command, **kwargs):
    destination = kwargs.pop('stdout', None)
    capture = kwargs.pop('capture_output', False) or destination is not None
    kwargs.pop('text', None)
    output = execute(command, kwargs.pop('env', os.environ.copy()), timeout=kwargs.pop('timeout', 180), capture=capture)
    if kwargs:
        raise ValueError('Unsupported contract runner options')
    if destination is not None:
        destination.write(output)
    return subprocess.CompletedProcess(command, 0, stdout=output)


def catalogue(expression=None):
    command = ['dotnet', 'run', '--project', str(PROJECT), '--no-build', '--no-restore', '--', '--pre-enumerate-theories', 'on', '--list-tests', 'json']
    if expression:
        command += ['--filter', expression]
    return read_catalogue(run(command, capture_output=True, text=True).stdout)


def main():
    install_cancellation_handler()
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
        run(['python3', 'scripts/check_contract_supply_chain.py'])
        run(['npm', 'run', 'check', '--prefix', str(PACKAGE)])
        files = sorted(str(p) for folder in ('generated', 'tests/generated') for p in (PACKAGE / folder).glob('*.json'))
        if len(files) != 5:
            raise ValueError('Expected two production, one dormant browser and two fixture documents')
        run(['node', str(PACKAGE / 'tooling/validate.mjs'), *files])
        run(['node', str(PACKAGE / 'tooling/baseline.mjs')])
        node_report = output / 'node.xml'
        with node_report.open('w') as stream:
            run(['node', '--test', '--test-reporter=junit', str(PACKAGE / 'tests/compatibility.test.mjs')], stdout=stream)
        if len(MATRIX['nodeIdentities']) != MATRIX['node']:
            raise ValueError('Node identity manifest count differs')
        require_node_report(node_report, MATRIX['nodeIdentities'])
    print(f'Contract {args.partition}: exact identities passed; no skipped or missing cases.')


if __name__ == '__main__':
    main()
