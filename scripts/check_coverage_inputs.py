#!/usr/bin/env python3
"""Coverage percentages cannot substitute for evidence from every selected matrix cell."""
import json
from pathlib import Path
import sys

root = Path(sys.argv[1])
matrix = json.loads(sys.argv[2])
labels = ['unit', 'PostgreSql', 'SqlServer'] + [cell['provider'] + '-' + cell['browser'] for cell in matrix['include']]
for label in labels:
    report = root / ('foundation-' + label) / 'coverage' / (label + '.cobertura.xml')
    if not report.is_file() or report.stat().st_size == 0:
        raise SystemExit('Missing required coverage input: ' + label)
print(f'All {len(labels)} required coverage inputs exist.')
