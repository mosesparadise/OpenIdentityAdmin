"""Strict native cohort validation and finite certificate-only failure projection."""
import datetime
import json
import re
import xml.etree.ElementTree as ET
from evidence import ZERO, EXIT
from failure import project
from common import TEST

TARGET = TEST + '(provider: "PostgreSql")'
TIME = re.compile(r'\d{4}-\d{2}-\d{2}T\d{2}:\d{2}:\d{2}\.\d{7}\+00:00')
DURATION = re.compile(r'\d{2}:\d{2}:\d{2}\.\d{7}')


def native(raw, selected, exit_code, historical):
    assert len(raw) <= 2 * 1024 * 1024 and b'<!DOCTYPE' not in raw.upper() and b'<!ENTITY' not in raw.upper()
    root = ET.fromstring(raw)
    summaries = root.findall('./{*}ResultSummary')
    rows = root.findall('./{*}Results/{*}UnitTestResult')
    definitions = root.findall('./{*}TestDefinitions/{*}UnitTest')
    entries = root.findall('./{*}TestEntries/{*}TestEntry')
    assert len(summaries) == 1 and len(rows) == len(definitions) == len(entries) == len(selected) == 30
    counters = summaries[0].findall('./{*}Counters')
    assert len(counters) == 1 and set(counters[0].attrib) == ZERO | {'total','executed','passed','failed'}
    counts = {k: int(v) for k, v in counters[0].attrib.items()}
    assert all(counts[k] == 0 for k in ZERO) and counts['total'] == counts['executed'] == 30
    expected = {item[0] for item in selected.values()}
    assert len(expected) == 30 and {r.get('testName') for r in rows} == expected
    assert expected == {r['name'] for r in historical['native_order']} and TARGET in expected
    defs = {d.get('id'): d for d in definitions}
    ent = {e.get('testId'): e for e in entries}
    assert len(defs) == len(ent) == len({r.get('testId') for r in rows}) == len({r.get('executionId') for r in rows}) == 30
    assert set(defs) == set(ent) == {r.get('testId') for r in rows}
    errors = []
    for row in rows:
        tid, eid = row.get('testId'), row.get('executionId')
        assert tid and eid and row.get('outcome') in ('Passed','Failed')
        definition, entry = defs[tid], ent[tid]
        assert definition.get('name') == row.get('testName')
        execution = definition.findall('./{*}Execution')
        assert len(execution) == 1 and execution[0].get('id') == entry.get('executionId') == eid
        own_errors = row.findall('./{*}Output/{*}ErrorInfo')
        assert len(own_errors) == int(row.get('outcome') == 'Failed')
        for error in own_errors:
            assert error.findtext('{*}Message') and error.findtext('{*}StackTrace')
        errors.extend(own_errors)
        assert TIME.fullmatch(row.get('startTime', '')) and TIME.fullmatch(row.get('endTime', ''))
        assert DURATION.fullmatch(row.get('duration', ''))
        datetime.datetime.fromisoformat(row.get('startTime'))
        datetime.datetime.fromisoformat(row.get('endTime'))
        assert row.get('endTime') >= row.get('startTime')
    assert root.findall('.//{*}ErrorInfo') == errors
    failed = sum(r.get('outcome') == 'Failed' for r in rows)
    assert counts['failed'] == failed and counts['passed'] == 30 - failed
    assert exit_code == (2 if failed else 0)
    infos = root.findall('.//{*}RunInfo')
    if failed:
        assert summaries[0].get('outcome') == 'Failed' and len(infos) == 1
        assert infos[0].get('outcome') == 'Error' and infos[0].findtext('{*}Text') == EXIT
    else:
        assert summaries[0].get('outcome') in ('Completed','Passed') and not infos
    ordered = sorted(rows, key=lambda r: r.get('startTime'))
    assert all(a.get('endTime') <= b.get('startTime') for a, b in zip(ordered, ordered[1:]))
    assert len({r.get('startTime') for r in rows}) == 30
    order = [{'ordinal': i + 1, 'name': r.get('testName'), 'outcome': r.get('outcome'),
              'start': r.get('startTime'), 'end': r.get('endTime'), 'duration': r.get('duration')}
             for i, r in enumerate(ordered)]
    names = [r['name'] for r in order]
    historical_names = [r['name'] for r in historical['native_order']]
    certificate = next(r for r in rows if r.get('testName') == TARGET)
    # Project only this validated row, never arbitrary other-case errors or console text.
    projected = ET.Element('TestRun'); results = ET.SubElement(projected, 'Results'); results.append(certificate)
    return {'counts': counts, 'outcome': 'Failed' if failed else 'Passed', 'report_valid': True,
            'order': order, 'historical_order_matches': names == historical_names,
            'historical_prefix_matches': names[:names.index(TARGET)] == historical_names[:historical_names.index(TARGET)],
            'certificate_ordinal': names.index(TARGET) + 1, 'certificate_outcome': certificate.get('outcome'),
            'certificate_failure': project(ET.tostring(projected)), 'acceptance': False}
