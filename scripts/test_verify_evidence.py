import tempfile
import unittest
from pathlib import Path
from test_evidence import require_complete_trx


class ReportValidationTests(unittest.TestCase):
    def check(self, attributes, **kwargs):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'result.trx'
            path.write_text(f'<TestRun xmlns="urn:trx"><ResultSummary><Counters {attributes}/></ResultSummary></TestRun>')
            return require_complete_trx(path, **kwargs)

    def test_nonempty_complete_pass(self):
        self.assertEqual(2, self.check('total="2" executed="2" passed="2"'))

    def test_zero_matches_cannot_pass(self):
        with self.assertRaises(ValueError):
            self.check('total="0" executed="0" passed="0"')

    def test_skipped_required_case_cannot_pass(self):
        with self.assertRaises(ValueError):
            self.check('total="2" executed="1" passed="1" notExecuted="1"')

    def test_failed_case_cannot_pass(self):
        with self.assertRaises(ValueError):
            self.check('total="2" executed="2" passed="1" failed="1"')

    def test_partial_selection_cannot_pass_even_when_every_selected_test_passes(self):
        with self.assertRaisesRegex(ValueError, "selection changed"):
            self.check('total="1" executed="1" passed="1"', expected_count=3)

    def test_expanded_selection_requires_matrix_review(self):
        with self.assertRaisesRegex(ValueError, "selection changed"):
            self.check('total="6" executed="6" passed="6"', expected_count=3)

    def test_expected_selection_passes(self):
        self.assertEqual(3, self.check('total="3" executed="3" passed="3"', expected_count=3))

    def test_missing_report_cannot_pass(self):
        with tempfile.TemporaryDirectory() as directory:
            with self.assertRaises(FileNotFoundError):
                require_complete_trx(Path(directory) / 'missing.trx')


if __name__ == '__main__':
    unittest.main()
