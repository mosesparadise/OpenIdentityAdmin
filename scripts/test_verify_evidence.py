import tempfile
import unittest
from pathlib import Path
from test_evidence import require_complete_trx


class ReportValidationTests(unittest.TestCase):
    def check(self, attributes):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'result.trx'
            path.write_text(f'<TestRun xmlns="urn:trx"><ResultSummary><Counters {attributes}/></ResultSummary></TestRun>')
            return require_complete_trx(path)

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


if __name__ == '__main__':
    unittest.main()
