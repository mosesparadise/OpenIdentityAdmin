"""Pure failure-propagation controls for the proposed hosted diagnostic."""
import unittest
from outcome import job_success


class OutcomeTests(unittest.TestCase):
    def setUp(self):
        self.args = [0, 0, 0, {'report_valid': True, 'outcome': 'Passed'},
                     {'diagnostic_complete': True, 'acceptance': False}, {'complete': True}]

    def test_complete_local_observation_can_succeed_without_acceptance(self):
        self.assertTrue(job_success(*self.args))

    def test_each_failed_exit_stays_red_even_if_others_succeed(self):
        for index in range(3):
            for exit_code in (1, 2, 124, -15, None, False, '0'):
                args = self.args.copy(); args[index] = exit_code
                self.assertFalse(job_success(*args))

    def test_captured_native_failure_cannot_be_green(self):
        self.args[3]['outcome'] = 'Failed'
        self.assertFalse(job_success(*self.args))

    def test_missing_or_invalid_receipt_fails(self):
        for index in (3, 4, 5):
            args = self.args.copy(); args[index] = {}
            self.assertFalse(job_success(*args))
        self.args[3]['report_valid'] = False
        self.assertFalse(job_success(*self.args))

    def test_incomplete_diagnostic_and_cleanup_fail(self):
        for index, key in ((4, 'diagnostic_complete'), (5, 'complete')):
            args = self.args.copy(); args[index] = {**args[index], key: False}
            self.assertFalse(job_success(*args))

    def test_acceptance_claim_is_rejected(self):
        self.args[4]['acceptance'] = True
        self.assertFalse(job_success(*self.args))


if __name__ == '__main__': unittest.main()
