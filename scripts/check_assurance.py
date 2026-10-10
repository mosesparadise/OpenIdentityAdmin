"""Validate assurance snapshot integrity, never product compliance or approval truth."""
import collections
import csv
from datetime import date
import hashlib
import json
from pathlib import Path, PurePosixPath
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
BASE = 'docs/assurance/'
# Reviewed release trust anchor, independent of editable catalogue metadata.
PIN = {
    'version': '5.0.0', 'tag': 'v5.0.0_release',
    'commit': '5cf9b032440be53ce345ab3c130fda46ba1ce7a2',
    'url': 'https://raw.githubusercontent.com/OWASP/ASVS/5cf9b032440be53ce345ab3c130fda46ba1ce7a2/5.0/docs_en/OWASP_Application_Security_Verification_Standard_5.0.0_en.csv',
    'path': BASE + 'asvs-5.0.0.csv',
    'sha256': '98c8fe911b9edb403af8ee05d3ce8201ecac2659e313b053890a62847cdcf680',
    'row_count': 345, 'chapter_count': 17, 'section_count': 80,
    'license': 'CC-BY-SA-4.0',
    'license_url': 'https://creativecommons.org/licenses/by-sa/4.0/',
}
SOURCE_FIELDS = 'chapter_id chapter_name section_id section_name req_id req_description L'.split()
FIELDS = 'control_id source_id source_version level triage applicability rationale owner requirements tasks threat_id component implementation automated_tests manual_assessment evidence evidence_date evidence_scope result residual_risk exception_approver exception_expiry exception_reason assessed_by reviewed_on reassessment_trigger'.split()
PAIRS = {('pending', 'not_assessed'), ('applicable', 'not_assessed'),
         ('applicable', 'pass'), ('applicable', 'fail'), ('applicable', 'exception'),
         ('not_applicable', 'not_applicable')}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def text(value):
    return isinstance(value, str) and bool(value.strip())


def unique_object(pairs):
    result = {}
    for key, value in pairs:
        require(key not in result, 'JSON duplicate key')
        result[key] = value
    return result


def read_json(path):
    try:
        result = json.loads(path.read_text(encoding='utf-8'), object_pairs_hook=unique_object)
    except json.JSONDecodeError as exc:
        raise ValueError('JSON syntax') from exc
    require(isinstance(result, dict), 'JSON object required')
    return result


def read_csv(path, fields, allow_blank_lines=False):
    try:
        with path.open(encoding='utf-8', newline='') as stream:
            reader = csv.reader(stream, strict=True)
            require(next(reader, None) == fields, 'CSV header mismatch')
            result = []
            for number, cells in enumerate(reader, 2):
                if not cells and allow_blank_lines:
                    continue
                require(len(cells) == len(fields), f'CSV row width at row {number}')
                result.append(dict(zip(fields, cells)))
            return result
    except csv.Error as exc:
        raise ValueError('CSV syntax') from exc


def tokens(value, label, required=False):
    require(isinstance(value, str), label + ' must be text')
    if not value:
        require(not required, label + ' required')
        return []
    items = value.split('|')
    require(all(text(item) and item == item.strip() for item in items), label + ' empty/whitespace token')
    require(len(items) == len(set(items)), label + ' duplicate token')
    return items


def local_file(root, value, label):
    path = PurePosixPath(value)
    require(text(value) and not path.is_absolute() and '..' not in path.parts
            and '\\' not in value and ':' not in value, label + ' invalid local path')
    resolved = (root / path).resolve()
    require(resolved.is_relative_to(root.resolve()) and resolved.is_file(), label + ' missing/escaping file')
    return resolved


def calendar(value, label):
    require(isinstance(value, str) and bool(re.fullmatch(r'\d{4}-\d{2}-\d{2}', value)), label + ' canonical date required')
    try:
        return date.fromisoformat(value)
    except ValueError as exc:
        raise ValueError(label + ' invalid date') from exc


def validate(root=ROOT, today=None):
    today = today or date.today()
    root = Path(root)
    manifest = read_json(root / BASE / 'catalogue.json')
    for key, expected in PIN.items():
        require(type(manifest.get(key)) is type(expected) and manifest[key] == expected, 'catalogue ' + key)
    require(text(manifest.get('source')) and text(manifest.get('attribution')), 'catalogue attribution/source')
    require(calendar(manifest.get('acquired_on', ''), 'catalogue acquired_on') <= today, 'catalogue acquired_on future')
    source_path = local_file(root, PIN['path'], 'catalogue path')
    require(hashlib.sha256(source_path.read_bytes()).hexdigest() == PIN['sha256'], 'source digest mismatch')
    # The immutable upstream release includes a trailing blank line.
    source = read_csv(source_path, SOURCE_FIELDS, allow_blank_lines=True)
    official = {row['req_id']: row for row in source}
    require(len(source) == len(official) == PIN['row_count'], 'source inventory')
    require(len({row['chapter_id'] for row in source}) == PIN['chapter_count'], 'source chapters')
    require(len({row['section_id'] for row in source}) == PIN['section_count'], 'source sections')

    roles = read_json(root / BASE / 'roles.json')
    require(bool(roles), 'role inventory empty')
    for key, role in roles.items():
        require(bool(re.fullmatch('[A-Z]+', key)) and isinstance(role, dict)
                and set(role) == {'responsibility', 'named_owner'}
                and text(role['responsibility'])
                and (role['named_owner'] is None or text(role['named_owner'])), 'role schema')
    threats = read_json(root / BASE / 'threats.json')
    require(bool(threats), 'threat inventory empty')
    for key, threat in threats.items():
        require(bool(re.fullmatch(r'THR-\d{2}', key)) and isinstance(threat, dict)
                and set(threat) == {'statement', 'assets', 'boundaries', 'owner'}
                and all(text(value) for value in threat.values())
                and threat['owner'] in roles, 'threat schema/owner')
    prd = re.sub(r'<!--[\s\S]*?-->', '', (root / 'docs/approved-v0.2/00-prd.md').read_text())
    requirements = set(re.findall(r'^\| ((?:N?FR)-\d{2}) \|', prd, re.MULTILINE))
    roadmap = re.sub(r'<!--[\s\S]*?-->', '', (root / 'docs/roadmap.md').read_text())
    definitions = re.findall(r'^\| (M\d{2}-[A-Z]) \|', roadmap, re.MULTILINE)
    # Milestone 010 deliberately uses a single prose declaration, not a table.
    definitions += re.findall(r'^(M10-A): ', roadmap, re.MULTILINE)
    require(len(definitions) == len(set(definitions)), 'duplicate task definition')
    tasks = set(definitions)
    require(bool(requirements) and bool(tasks), 'requirement/task inventory empty')
    rows = read_csv(root / BASE / 'controls.csv', FIELDS)
    seen = set()
    results = collections.Counter()
    for number, row in enumerate(rows, 2):
        label = f'control row {number}: '
        sid = row['source_id']
        require(sid not in seen, label + 'duplicate source_id')
        require(sid in official, label + 'unknown source_id')
        seen.add(sid)
        for field, expected in [('control_id', 'v5.0.0-' + sid[1:]), ('source_version', PIN['version']), ('level', official[sid]['L'])]:
            require(row[field] == expected, label + field)
        require(row['triage'] in {'expected', 'conditional', 'mixed', 'deployment'}, label + 'triage')
        for field in ['rationale', 'component', 'reassessment_trigger']:
            require(text(row[field]), label + field + ' required')
        require(row['owner'] in roles, label + 'owner')
        require(row['threat_id'] in threats, label + 'threat_id')
        for field, inventory in [('requirements', requirements), ('tasks', tasks)]:
            require(set(tokens(row[field], label + field, required=True)) <= inventory, label + field + ' unknown reference')
        for field in ['implementation', 'evidence']:
            for item in tokens(row[field], label + field):
                local_file(root, item, label + field)
        tokens(row['automated_tests'], label + 'automated_tests')
        dates = {}
        for field in ['evidence_date', 'reviewed_on', 'exception_expiry']:
            if row[field]:
                dates[field] = calendar(row[field], label + field)
                if field != 'exception_expiry':
                    require(dates[field] <= today, label + field + ' future')
        result = row['result']
        require((row['applicability'], result) in PAIRS, label + 'state pair')
        if result != 'not_assessed':
            for field in ['evidence', 'evidence_date', 'evidence_scope', 'assessed_by', 'reviewed_on']:
                require(text(row[field]), label + field + ' required')
            require(dates['reviewed_on'] >= dates['evidence_date'], label + 'assessment chronology')
        if result == 'pass':
            require(text(row['implementation']), label + 'implementation required')
            require(text(row['automated_tests']) or text(row['manual_assessment']), label + 'assessment method required')
        if result in {'exception', 'not_applicable'}:
            for field in ['exception_approver', 'exception_reason']:
                require(text(row[field]), label + field + ' required')
            if result == 'exception':
                require(text(row['residual_risk']), label + 'residual_risk required')
                require('exception_expiry' in dates and dates['exception_expiry'] >= today, label + 'exception_expiry missing/expired')
            else:
                require(not row['exception_expiry'], label + 'exclusion expiry must be empty')
        else:
            require(not any(row[field] for field in ['exception_approver', 'exception_reason', 'exception_expiry']), label + 'stale exception metadata')
        results[result] += 1
    require(seen == set(official), 'control inventory incomplete')
    return results


def main():
    try:
        results = validate()
    except (ValueError, OSError, UnicodeError, RuntimeError) as exc:
        # Do not dump records or OS exception details containing evidence paths.
        message = str(exc) if isinstance(exc, ValueError) and not isinstance(exc, UnicodeError) else 'unreadable/invalid input'
        print('FAIL: ' + message, file=sys.stderr)
        return 1
    print('PASS: 345 controls; snapshot integrity, not compliance. Results: ' + json.dumps(dict(sorted(results.items()))))
    return 0


if __name__ == '__main__':
    sys.exit(main())
