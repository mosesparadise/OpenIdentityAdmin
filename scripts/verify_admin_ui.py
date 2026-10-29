#!/usr/bin/env python3
"""Verify finite UI tests, deterministic shipped bytes and the reviewed dependency graph."""
import json
import os
from pathlib import Path
from check_contract_supply_chain import inventory_toolchain
from contract_evidence import require_node_report
from verify_contrast_experiment import execute, install_cancellation_handler

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / 'src/frontend/IdentityPlatform.Admin.Ui'


def main():
    install_cancellation_handler()
    inventory = inventory_toolchain(PACKAGE, ROOT / 'eng/admin-ui')
    for command in ('check', 'test'):
        execute(['npm', 'run', command, '--prefix', str(PACKAGE)], os.environ.copy(), timeout=120)
    reports = {'session': 'test-identities.json', 'applications': 'application-test-identities.json'}
    for report, inventory_file in reports.items():
        require_node_report(PACKAGE / f'test-results/{report}.xml', json.loads((ROOT / 'eng/admin-ui' / inventory_file).read_text()))
    audit = json.loads(execute(['npm', 'audit', '--prefix', str(PACKAGE), '--json'], os.environ.copy(), timeout=60, capture=True))
    if audit.get('auditReportVersion') != 2 or audit.get('vulnerabilities') != {} or audit.get('metadata', {}).get('vulnerabilities', {}).get('total') != 0:
        raise ValueError('UI advisory evidence is incomplete or contains vulnerabilities')
    output = ROOT / 'artifacts/admin-ui'
    output.mkdir(parents=True, exist_ok=True)
    (output / 'npm-audit.json').write_text(json.dumps(audit, indent=2) + '\n')
    (output / 'npm-inventory.json').write_text(json.dumps(inventory, indent=2) + '\n')
    for report in reports:
        for extension in ('xml', 'tap'):
            name = f'{report}.{extension}'
            (output / name).write_bytes((PACKAGE / 'test-results' / name).read_bytes())
    print(f'PASS: UI source/shipped bytes, eleven session and nine application controls and {len(inventory)} locked dependency entries')


if __name__ == '__main__':
    main()
