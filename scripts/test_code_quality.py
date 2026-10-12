"""Prove legacy inventory checks reject silent expansion and rebinding."""
import unittest
from control_runner import run, is_complete
from check_code_quality import require_no_growth


class BaselineTests(unittest.TestCase):
    """Keep legacy-debt admission distinct from reviewed exceptions."""

    def test_Given_existing_debt_When_removed_Then_inventory_shrinks(self):
        """Unchanged entries and explicit deletion are supported cleanup operations."""
        entry = {'Finding': {'Rule': 'CQ001', 'Path': 'tests/a.cs', 'Symbol': 'A.Run()', 'Fingerprint': 'old'}}
        require_no_growth([entry], [entry])
        require_no_growth([], [entry])

    def test_Given_new_or_rebound_debt_When_compared_Then_gate_rejects(self):
        """Reject each changed identity component, duplicates and a new finding on first adoption."""
        finding = {'Rule': 'CQ001', 'Path': 'tests/a.cs', 'Symbol': 'A.Run()', 'Fingerprint': 'old'}
        old = [{'Finding': finding}]
        for field in finding:
            with self.subTest(field=field), self.assertRaises(ValueError):
                require_no_growth([{'Finding': dict(finding, **{field: 'changed'})}], old)
        with self.assertRaises(ValueError):
            require_no_growth(old + old, old)
        with self.assertRaises(ValueError):
            require_no_growth(old, [])

    def test_Given_incomplete_control_results_When_evaluated_Then_acceptance_fails(self):
        """Reject zero, missing, skipped, expected-failure and unexpectedly successful controls."""
        result = unittest.TestResult()
        self.assertFalse(is_complete(result, 1))
        result.testsRun = 1
        self.assertTrue(is_complete(result, 1))
        self.assertFalse(is_complete(result, 0))
        self.assertFalse(is_complete(result, 2))
        for field in ('skipped', 'expectedFailures', 'unexpectedSuccesses', 'failures', 'errors'):
            setattr(result, field, [('synthetic', 'not accepted')])
            self.assertFalse(is_complete(result, 1))
            setattr(result, field, [])


if __name__ == '__main__':
    run(3)
