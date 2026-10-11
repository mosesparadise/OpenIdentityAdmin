"""Permanent negative controls for the offline assurance register gate."""
import copy
import csv
import hashlib
import json
import re
from pathlib import Path
import shutil
import subprocess
import sys
import tempfile
import unittest
from datetime import date, timedelta

from check_assurance import local_file, validate

ROOT = Path(__file__).resolve().parents[1]
# Capture once: synthetic boundary cases stay relative to the same validation clock,
# while real dated evidence must remain valid as the register evolves.
TODAY = date.today()


def clean_result(result):
    return result.wasSuccessful() and result.testsRun > 0 and not result.skipped and not result.expectedFailures


class AssuranceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        shutil.copytree(ROOT / 'docs/assurance', self.root / 'docs/assurance')
        (self.root / 'docs/approved-v0.2').mkdir()
        for path in ['docs/roadmap.md', 'docs/approved-v0.2/00-prd.md']:
            shutil.copyfile(ROOT / path, self.root / path)
        (self.root / 'synthetic evidence.md').write_text('Synthetic evidence fixture only.\n')
        self.path = self.root / 'docs/assurance/controls.csv'
        with self.path.open(newline='') as stream:
            reader = csv.DictReader(stream)
            self.fields = reader.fieldnames
            self.rows = list(reader)
        references = {value for row in self.rows for field in ['implementation', 'evidence']
                      for value in row[field].split('|') if value}
        for reference in references:
            source = local_file(ROOT, reference, 'fixture reference')
            destination = self.root / reference
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(source, destination)
        self.assertEqual(345, validate(self.root, TODAY)['not_assessed'])

    def save(self, rows=None):
        with self.path.open('w', newline='') as stream:
            writer = csv.DictWriter(stream, fieldnames=self.fields, lineterminator='\n')
            writer.writeheader()
            writer.writerows(self.rows if rows is None else rows)

    def reject(self, message):
        with self.assertRaisesRegex(ValueError, message):
            validate(self.root, TODAY)

    def assessed(self, result='pass'):
        row = copy.deepcopy(self.rows[0])
        row.update(applicability='applicable', result=result,
                   implementation='synthetic evidence.md', evidence='synthetic evidence.md',
                   evidence_date=TODAY.isoformat(), evidence_scope='Synthetic revision/configuration; no product assessment.',
                   assessed_by='Synthetic reviewer', reviewed_on=TODAY.isoformat(),
                   automated_tests='SYNTHETIC-001')
        if result in {'exception', 'not_applicable'}:
            row.update(exception_approver='Synthetic approver', exception_reason='Synthetic decision only.')
        if result == 'exception':
            row['exception_expiry'] = TODAY.isoformat()
        if result == 'not_applicable':
            row['applicability'] = 'not_applicable'
        return row

    def test_real_baseline_is_pending_and_unchanged(self):
        self.assertEqual({'not_assessed': 345}, dict(validate(ROOT, TODAY)))

    def test_required_runner_rejects_zero_skip_and_expected_failure(self):
        result = unittest.TestResult()
        self.assertFalse(clean_result(result))
        result.testsRun = 1
        self.assertTrue(clean_result(result))
        for field in ['skipped', 'expectedFailures', 'failures', 'errors', 'unexpectedSuccesses']:
            with self.subTest(field=field):
                setattr(result, field, [('synthetic', 'synthetic')])
                self.assertFalse(clean_result(result))
                setattr(result, field, [])

    def test_protocol_sections_have_their_actual_delivery_owners(self):
        controls = {row['source_id']: row for row in self.rows}
        for key in ['V10.7.1', 'V10.7.2', 'V10.7.3']:
            self.assertIn('FR-08', controls[key]['requirements'].split('|'))
            self.assertIn('M06-A', controls[key]['tasks'].split('|'))
            self.assertIn('M06-C', controls[key]['tasks'].split('|'))
        for key in ['V10.3.' + str(n) for n in range(1, 6)]:
            self.assertIn('M02-D', controls[key]['tasks'].split('|'))
            self.assertIn('M03-C', controls[key]['tasks'].split('|'))
        self.assertIn('M07-C', controls['V10.6.2']['tasks'].split('|'))
        self.assertIn('M05-D', controls['V10.5.1']['tasks'].split('|'))

    def test_source_corruption_is_rejected(self):
        path = self.root / 'docs/assurance/asvs-5.0.0.csv'
        path.write_bytes(path.read_bytes() + b'\n')
        self.reject('source digest')

    def test_coordinated_source_and_manifest_replacement_is_rejected(self):
        path = self.root / 'docs/assurance/asvs-5.0.0.csv'
        path.write_bytes(path.read_bytes() + b'\n')
        manifest = self.root / 'docs/assurance/catalogue.json'
        obj = json.loads(manifest.read_text())
        obj['sha256'] = hashlib.sha256(path.read_bytes()).hexdigest()
        manifest.write_text(json.dumps(obj))
        self.reject('catalogue sha256')

    def test_manifest_identity_cannot_change(self):
        path = self.root / 'docs/assurance/catalogue.json'
        original = json.loads(path.read_text())
        for key in ['source', 'attribution', 'version', 'commit', 'tag', 'url', 'path', 'row_count', 'section_count', 'chapter_count', 'license', 'license_url']:
            with self.subTest(key=key):
                obj = dict(original)
                obj[key] = 'changed'
                path.write_text(json.dumps(obj))
                self.reject('catalogue ' + key)
        path.write_text(json.dumps(original))

    def test_missing_duplicate_and_unknown_controls(self):
        self.save(self.rows[:-1])
        self.reject('control inventory')
        self.save(self.rows + [self.rows[0]])
        self.reject('duplicate source_id')
        changed = copy.deepcopy(self.rows)
        changed[0]['source_id'] = 'V99.1.1'
        self.save(changed)
        self.reject('unknown source_id')

    def test_exact_control_identity_version_and_level(self):
        for field, value in [('control_id', 'v5.0.0-99.1.1'), ('source_version', '4.0.0'), ('level', '3')]:
            with self.subTest(field=field):
                rows = copy.deepcopy(self.rows)
                rows[0][field] = value
                self.save(rows)
                self.reject(field)

    def test_headers_and_row_width(self):
        self.save()
        original = self.path.read_text()
        for changed in [original.replace('control_id,', '', 1), original.replace('control_id,', 'control_id,control_id,', 1), original + 'short,row\n', original + ','.join(['x'] * (len(self.fields) + 1)) + '\n']:
            with self.subTest(changed=changed[:40]):
                self.path.write_text(changed)
                self.reject('header|row width')

    def test_malformed_csv_quoting(self):
        self.path.write_text(','.join(self.fields) + '\n"unterminated')
        self.reject('CSV')

    def test_json_parsing_and_shape(self):
        path = self.root / 'docs/assurance/roles.json'
        for content in ['{"ENG":1,"ENG":2}', '{', '[]', '{"ENG":null}']:
            with self.subTest(content=content):
                path.write_text(content)
                self.reject('JSON|role')

    def test_exact_foreign_references_and_multivalues(self):
        for field, value in [('owner', 'unknown'), ('threat_id', 'unknown'), ('requirements', 'FR-2'), ('tasks', 'M99-Z'), ('requirements', 'FR-25|'), ('tasks', '|M01-C'), ('requirements', 'FR-25||FR-22'), ('requirements', 'FR-25|FR-25'), ('requirements', 'FR-25| '), ('triage', 'unknown')]:
            with self.subTest(field=field, value=value):
                rows = copy.deepcopy(self.rows)
                rows[0][field] = value
                self.save(rows)
                self.reject(field)

    def test_task_mentions_cannot_replace_deleted_definitions(self):
        path = self.root / 'docs/roadmap.md'
        path.write_text(re.sub(r'^\| M01-C \|.*\n', '', path.read_text(), flags=re.MULTILINE))
        self.reject('tasks unknown reference')

    def test_duplicate_task_definitions_are_rejected(self):
        path = self.root / 'docs/roadmap.md'
        path.write_text(path.read_text() + '\n| M01-C | Duplicate | Invalid duplicate definition |\n')
        self.reject('duplicate task definition')

    def test_comment_only_task_inventory_is_rejected(self):
        path = self.root / 'docs/roadmap.md'
        path.write_text('<!--\n' + path.read_text() + '\n-->\n')
        self.reject('requirement/task inventory empty')

    def test_extended_milestone_prose_definition_is_supported(self):
        rows = copy.deepcopy(self.rows)
        rows[0]['tasks'] = 'M10-A'
        self.save(rows)
        self.assertEqual(345, validate(self.root, TODAY)['not_assessed'])

    def test_valid_multivalues_and_unicode(self):
        rows = copy.deepcopy(self.rows)
        rows[0].update(requirements='FR-25|NFR-13', tasks='M01-C|M09-D', rationale='Pending review — café.')
        self.save(rows)
        self.assertEqual(345, validate(self.root, TODAY)['not_assessed'])

    def test_paths_must_be_contained_files_even_while_pending(self):
        outside = tempfile.TemporaryDirectory()
        self.addCleanup(outside.cleanup)
        (Path(outside.name) / 'file').write_text('Synthetic existing outside evidence.')
        (self.root / 'outside-link').symlink_to(outside.name)
        (self.root / 'broken-link').symlink_to(self.root / 'missing')
        for value in ['../escape', '/etc/passwd', 'docs', 'missing', 'https://example.com/proof', 'outside-link/file', 'broken-link', 'C:\\secret']:
            with self.subTest(value=value):
                rows = copy.deepcopy(self.rows)
                rows[0]['evidence'] = value
                self.save(rows)
                self.reject('evidence')

    def test_all_applicability_result_pairs(self):
        allowed = {('pending', 'not_assessed'), ('applicable', 'not_assessed'), ('applicable', 'pass'), ('applicable', 'fail'), ('applicable', 'exception'), ('not_applicable', 'not_applicable')}
        for applicability in ['pending', 'applicable', 'not_applicable', 'unknown']:
            for result in ['not_assessed', 'pass', 'fail', 'exception', 'not_applicable', 'unknown']:
                with self.subTest(applicability=applicability, result=result):
                    rows = copy.deepcopy(self.rows)
                    row = self.assessed(result) if result != 'not_assessed' else rows[0]
                    row.update(applicability=applicability, result=result)
                    rows[0] = row
                    self.save(rows)
                    if (applicability, result) in allowed:
                        self.assertEqual(345, sum(validate(self.root, TODAY).values()))
                    else:
                        self.reject('state pair')

    def test_pass_requires_every_evidence_field(self):
        for field in ['implementation', 'evidence', 'evidence_date', 'evidence_scope', 'assessed_by', 'reviewed_on', 'automated_tests']:
            with self.subTest(field=field):
                rows = copy.deepcopy(self.rows)
                rows[0] = self.assessed()
                rows[0][field] = ''
                self.save(rows)
                self.reject(field if field != 'automated_tests' else 'assessment method')

    def test_manual_assessment_is_a_valid_alternative(self):
        rows = copy.deepcopy(self.rows)
        rows[0] = self.assessed()
        rows[0].update(automated_tests='', manual_assessment='Synthetic manual procedure')
        self.save(rows)
        self.assertEqual(1, validate(self.root, TODAY)['pass'])

    def test_exclusion_and_exception_require_approval(self):
        for result in ['exception', 'not_applicable']:
            for field in ['exception_approver', 'exception_reason', 'assessed_by', 'evidence', 'evidence_scope']:
                with self.subTest(result=result, field=field):
                    rows = copy.deepcopy(self.rows)
                    rows[0] = self.assessed(result)
                    rows[0][field] = ''
                    self.save(rows)
                    self.reject(field)

    def test_exception_expiry_and_risk(self):
        for field, value in [('exception_expiry', ''), ('exception_expiry', (TODAY - timedelta(days=1)).isoformat()), ('residual_risk', '')]:
            with self.subTest(field=field, value=value):
                rows = copy.deepcopy(self.rows)
                rows[0] = self.assessed('exception')
                rows[0][field] = value
                self.save(rows)
                self.reject(field)

    def test_stale_exception_metadata_is_rejected(self):
        for result in ['not_assessed', 'pass', 'fail']:
            rows = copy.deepcopy(self.rows)
            rows[0] = self.assessed(result) if result != 'not_assessed' else rows[0]
            rows[0]['exception_approver'] = 'Stale'
            self.save(rows)
            self.reject('exception metadata')
        rows[0] = self.assessed('not_applicable')
        rows[0]['exception_expiry'] = TODAY.isoformat()
        self.save(rows)
        self.reject('exclusion expiry')

    def test_dates_are_canonical_real_and_not_future(self):
        for field in ['evidence_date', 'reviewed_on']:
            for value in ['20261010', '2026-02-30', (TODAY + timedelta(days=1)).isoformat(), '2026-1-1']:
                with self.subTest(field=field, value=value):
                    rows = copy.deepcopy(self.rows)
                    rows[0][field] = value
                    self.save(rows)
                    self.reject(field)
        rows = copy.deepcopy(self.rows)
        rows[0]['reviewed_on'] = '2024-02-29'
        self.save(rows)
        self.assertEqual(345, validate(self.root, TODAY)['not_assessed'])

    def test_assessment_cannot_precede_evidence(self):
        rows = copy.deepcopy(self.rows)
        rows[0] = self.assessed()
        rows[0]['reviewed_on'] = (TODAY - timedelta(days=1)).isoformat()
        self.save(rows)
        self.reject('chronology')

    def test_cli_reports_integrity_not_compliance_and_missing_input_fails(self):
        script = ROOT / 'scripts/check_assurance.py'
        good = subprocess.run([sys.executable, str(script)], capture_output=True, text=True, timeout=10)
        self.assertEqual(0, good.returncode, good.stderr)
        self.assertIn('integrity, not compliance', good.stdout)
        missing_root = self.root / 'empty'
        (missing_root / 'scripts').mkdir(parents=True)
        shutil.copyfile(script, missing_root / 'scripts/check_assurance.py')
        bad = subprocess.run([sys.executable, str(missing_root / 'scripts/check_assurance.py')], capture_output=True, text=True, timeout=10)
        self.assertNotEqual(0, bad.returncode)
        self.assertIn('FAIL:', bad.stderr)
        self.assertNotIn('Traceback', bad.stderr)


if __name__ == '__main__':
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(AssuranceTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    sys.exit(0 if clean_result(result) else 1)
