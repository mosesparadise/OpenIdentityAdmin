#!/usr/bin/env python3
"""Coverage percentages cannot substitute for evidence from every selected matrix cell."""
import json
from pathlib import Path
import sys


def require_inputs(root, matrix):
    labels = ['administrator-PostgreSql-bootstrap', 'administrator-SqlServer-bootstrap', 'administrator-PostgreSql-factors', 'administrator-SqlServer-factors', 'contracts-core', 'contracts-chromium', 'contracts-firefox', 'contracts-webkit', 'unit', 'PostgreSql', 'SqlServer', 'PostgreSql-lifecycle-restart', 'SqlServer-lifecycle-restart',
              'PostgreSql-lifecycle-distribution', 'SqlServer-lifecycle-distribution',
              'PostgreSql-lifecycle-keyrotation-activation', 'SqlServer-lifecycle-keyrotation-activation',
              'PostgreSql-lifecycle-keyrotation-retirement', 'SqlServer-lifecycle-keyrotation-retirement'] + [cell['provider'] + '-' + cell['browser'] for cell in matrix['include']]
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
