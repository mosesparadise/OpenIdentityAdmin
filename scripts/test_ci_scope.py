"""Independent controls for documentation selection and final-job acceptance."""
import subprocess
import unittest
from control_runner import run
from unittest.mock import patch
from ci_scope import ALWAYS_JOBS, FULL_JOBS, changed_paths, event_paths, require_acceptance, select


EXPECTED_ALWAYS = frozenset(('scope', 'assurance', 'source-secrets'))
EXPECTED_FULL = frozenset(('browser-plan', 'static-analysis', 'supply-chain', 'verify', 'browser',
                           'contrast-experiment', 'mutations', 'administrator-mutations', 'governance-mutations', 'administrator-protocol-mutations', 'contracts', 'coverage'))


class ScopeTests(unittest.TestCase):
    """Ensure prose selection cannot hide executable or incomplete required work."""

    def test_Given_only_prose_When_selecting_Then_lightweight_checks_apply(self):
        """Eligible added/edited/deleted prose does not require application execution."""
        for event in ('pull_request', 'push'):
            self.assertEqual('docs', select(event, ['README.md', 'docs/a.md', 'specs/009/tasks.md']))

    def test_Given_nonprose_or_mixed_paths_When_selecting_Then_full_checks_apply(self):
        """Policy, configuration, source and hidden/traversal paths cannot qualify as prose."""
        for path in ('src/a.cs', '.agents/rule.md', 'AGENTS.md', '.specify/memory/constitution.md',
                     '.github/workflows/a.yml', 'docs/a.json', 'eng/tool.cs', 'scripts/a.py',
                     'docs/engineering/code-quality.md', 'docs/approved-v0.2/00-prd.md', 'docs/amendments/A-001.md',
                     'docs/approval.md', 'tests/a.cs', 'docs/../src/a.md', 'docs/.hidden.md', 'docs/a\nb.md'):
            self.assertEqual('full', select('pull_request', ['README.md', path]))

    def test_Given_no_trustworthy_diff_When_selecting_Then_full_checks_apply(self):
        """Manual, scheduled, unknown, empty and unavailable evidence fails closed."""
        for event, paths in (('schedule', ['README.md']), ('workflow_dispatch', ['README.md']),
                             ('unknown', ['README.md']), ('push', []), ('pull_request', None)):
            self.assertEqual('full', select(event, paths))
        self.assertIsNone(event_paths('push', {}))
        with patch('ci_scope.changed_paths', side_effect=subprocess.CalledProcessError(1, 'git')):
            self.assertIsNone(event_paths('push', {'before': 'a', 'after': 'b'}))

    def test_Given_renamed_code_When_reading_diff_Then_both_paths_remain_visible(self):
        """Disable rename detection and preserve NUL-delimited Unicode/newline names."""
        paths = ['src/old.cs', 'docs/new.md', 'tests/é\ncase.cs']
        with patch('ci_scope.subprocess.check_output', return_value=''.join(':100644 100644 a b M\0' + path + '\0' for path in paths)) as command:
            self.assertEqual(paths, changed_paths('a' * 40, 'b' * 40))
            self.assertIn('--no-renames', command.call_args.args[0])
            self.assertIn('-z', command.call_args.args[0])
            self.assertEqual('full', select('pull_request', paths))
        with self.assertRaises(ValueError):
            changed_paths('--invalid', 'b' * 40)

    def test_Given_executable_or_symlink_prose_When_reading_modes_Then_full_checks_apply(self):
        """Treat executable, symlink, submodule and malformed diff evidence as requiring full verification."""
        for mode in ('100755', '120000', '160000'):
            for header in (f':100644 {mode} a b M', f':{mode} 100644 a b M'):
                with patch('ci_scope.subprocess.check_output', return_value=header + '\0docs/a.md\0'):
                    self.assertEqual('full', select('push', changed_paths('a' * 40, 'b' * 40)))
        for malformed in ('broken', ':100644 100644 a b R100\0docs/a.md\0', 'bad\0docs/a.md\0'):
            with patch('ci_scope.subprocess.check_output', return_value=malformed):
                with self.assertRaises(ValueError):
                    changed_paths('a' * 40, 'b' * 40)

    def test_Given_exact_job_results_When_accepting_Then_both_scopes_succeed(self):
        """Intentional docs-only omission is distinct from full test success."""
        self.assertEqual(EXPECTED_ALWAYS, ALWAYS_JOBS)
        self.assertEqual(EXPECTED_FULL, FULL_JOBS)
        for scope in ('docs', 'full'):
            require_acceptance(scope, self.results(scope))

    def test_Given_failed_or_missing_work_When_accepting_Then_gate_rejects(self):
        """Each required job and all failure/skip substitutions are independently checked."""
        for scope in ('docs', 'full'):
            for job in EXPECTED_ALWAYS | EXPECTED_FULL:
                results = self.results(scope)
                del results[job]
                with self.assertRaises(ValueError):
                    require_acceptance(scope, results)
                for invalid in ('failure', 'cancelled', None, 'queued'):
                    results = self.results(scope)
                    results[job]['result'] = invalid
                    with self.assertRaises(ValueError):
                        require_acceptance(scope, results)
                results = self.results(scope)
                results[job]['result'] = 'success' if results[job]['result'] == 'skipped' else 'skipped'
                with self.assertRaises(ValueError):
                    require_acceptance(scope, results)
        with self.assertRaises(ValueError):
            require_acceptance('unknown', self.results('full'))

    @staticmethod
    def results(scope):
        """Construct explicit expected outcomes without reading production selection logic."""
        return {job: {'result': 'skipped' if scope == 'docs' and job in EXPECTED_FULL else 'success'}
                for job in EXPECTED_ALWAYS | EXPECTED_FULL}


if __name__ == '__main__':
    run(7)
