#!/usr/bin/env python3
"""Coverage percentages cannot substitute for evidence from every selected matrix cell."""
import json
from pathlib import Path
import sys
from browser_matrix import ENGINES, CONTRACT_PARTITIONS


def browser_labels(matrix):
    """Reject incomplete or relabelled browser obligations before inspecting any coverage file."""
    cells = matrix.get('include')
    if not isinstance(cells, list) or not cells:
        raise ValueError('Missing browser coverage obligations')
    expected_keys = {'provider', 'browser', 'partition', 'label', 'browser_story'}
    if any(not isinstance(cell, dict) or set(cell) != expected_keys for cell in cells):
        raise ValueError('Unexpected browser coverage cell shape')
    engines = {cell['browser'] for cell in cells}
    if engines != set(ENGINES):
        raise ValueError('Unreviewed browser coverage engine set')
    expected = {(provider, engine, partition, story) for provider in ('PostgreSql', 'SqlServer')
                for engine in engines for partition in ('standard', 'administrator')
                for story in (('Accounts', 'Security') if partition == 'administrator' else ('',))}
    actual = [(cell['provider'], cell['browser'], cell['partition'], cell['browser_story']) for cell in cells]
    if len(actual) != len(expected) or set(actual) != expected:
        raise ValueError('Browser coverage provider/partition obligations are incomplete')
    for cell in cells:
        label = ('administrator-' if cell['partition'] == 'administrator' else '') + cell['provider'] + '-' + cell['browser'] + ('-' + cell['browser_story'].lower() if cell['browser_story'] else '')
        if cell['label'] != label:
            raise ValueError('Browser coverage label does not match its obligation')
    return [cell['label'] for cell in cells]


def require_inputs(root, matrix):
    """Require every independent execution report, including both issuer groups on both providers."""
    labels = ['administrator-PostgreSql-protocoladmission', 'administrator-SqlServer-protocoladmission', 'issuer-PostgreSql-flows', 'issuer-SqlServer-flows', 'issuer-PostgreSql-boundaries', 'issuer-SqlServer-boundaries', 'administrator-PostgreSql-recovery', 'administrator-SqlServer-recovery', 'administrator-PostgreSql-security', 'administrator-SqlServer-security', 'administrator-PostgreSql-accounts', 'administrator-SqlServer-accounts', 'administrator-PostgreSql-ceremonies', 'administrator-SqlServer-ceremonies', 'administrator-PostgreSql-bootstrap', 'administrator-SqlServer-bootstrap', 'administrator-PostgreSql-factors', 'administrator-SqlServer-factors', 'administrator-PostgreSql-governance-policy', 'administrator-PostgreSql-governance-commands', 'administrator-PostgreSql-governance-failures', 'administrator-SqlServer-governance-policy', 'administrator-SqlServer-governance-commands', 'administrator-SqlServer-governance-failures', 'unit', 'PostgreSql', 'SqlServer', 'PostgreSql-lifecycle-restart', 'SqlServer-lifecycle-restart',
              'PostgreSql-lifecycle-distribution', 'SqlServer-lifecycle-distribution',
              'PostgreSql-lifecycle-keyrotation-activation', 'SqlServer-lifecycle-keyrotation-activation',
              'PostgreSql-lifecycle-keyrotation-retirement', 'SqlServer-lifecycle-keyrotation-retirement'] + ['contracts-' + partition for partition in CONTRACT_PARTITIONS] + browser_labels(matrix)
    if len(labels) != len(set(labels)):
        raise ValueError('Duplicate required coverage cell')
    for label in labels:
        report = root / ('foundation-' + label) / 'coverage' / (label + '.cobertura.xml')
        if not report.is_file() or report.stat().st_size == 0:
            raise ValueError('Missing required coverage input: ' + label)
    return len(labels)


if __name__ == '__main__':
    count = require_inputs(Path(sys.argv[1]), json.loads(sys.argv[2]))
    print(f'All {count} required coverage inputs exist.')
