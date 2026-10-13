"""Reject substituted focused identities and incomplete or nonoverlapping browser evidence."""
from pathlib import Path
import os
import subprocess
import sys
import tempfile
import unittest
from xml.sax.saxutils import escape
from verify_history_experiment import TEST, NAMESPACE, require_selection, require_completion, prepare_reports, run_arms, MODE_VARIABLE


class HistoryExperimentTests(unittest.TestCase):
    def test_Given_NativeCatalogue_When_SelectionChanges_Then_SubstitutionIsRejected(self):
        """The exact UID, identity, provider and browser-story tuple are all required."""
        correct = {'a': (TEST + '(provider: "SqlServer")', 'SqlServer', True, NAMESPACE, 'Accounts')}
        require_selection(correct, correct, 'SqlServer')
        for selected in ({}, {'b': correct['a']}, correct | {'b': correct['a']}, {'a': correct['a'][:-1] + ('Security',)}):
            with self.assertRaises(ValueError):
                require_selection(correct, selected, 'SqlServer')
        with self.assertRaises(ValueError):
            require_selection(correct, correct, 'PostgreSql')

    def test_Given_FixedArms_When_EvidenceIsMissingOrNonoverlapping_Then_CompletionIsRejected(self):
        """Both valid controls pass while duplicate, malformed and unobserved delivery/window claims fail."""
        for arm in ('isolated', 'overlap'):
            marker = f'HISTORY_EXPERIMENT mode={arm} provider=SqlServer completed=True overlap={arm == "overlap"}'
            graph = 'graph-start=-1 database-running=-1 graph-ready=-1' if arm == 'isolated' else 'graph-start=0 database-running=10 graph-ready=90'
            window = f'HISTORY_WINDOW unit=microseconds {graph} positive-start=20 positive-end=30'
            progress = 'history-readiness elapsed-ms=2 requested=1 released=True fulfilled=True loads=1 safe=True cleanup-failed=False phase=Complete'
            cleanup = ['history-graph cleanup-completed=True'] if arm == 'overlap' else []
            lines = [marker, window, progress] + cleanup
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'result.trx'

                def check(values, passed=True, skipped=False):
                    """Write an independently specified minimal result envelope for the real strict parser."""
                    name = TEST + '(provider: "SqlServer")'
                    counters = ' '.join(f'{key}="0"' for key in ('error', 'timeout', 'aborted', 'inconclusive',
                        'passedButRunAborted', 'notRunnable', 'disconnected', 'warning', 'completed', 'inProgress', 'pending'))
                    from xml.sax.saxutils import quoteattr
                    path.write_text(f'<TestRun><Results><UnitTestResult testName={quoteattr(name)} testId="test" executionId="execution" outcome="' + ('Passed' if passed else 'Failed') + '"><Output><StdOut>' +
                                    escape('\n'.join(values)) + '</StdOut></Output></UnitTestResult></Results>' +
                                    f'<TestDefinitions><UnitTest id="test" name={quoteattr(name)}><Execution id="execution"/></UnitTest></TestDefinitions>' +
                                    '<TestEntries><TestEntry testId="test" executionId="execution"/></TestEntries>' +
                                    f'<ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="{int(passed)}" failed="{int(not passed)}" notExecuted="{int(skipped)}" {counters}/></ResultSummary></TestRun>')
                    require_completion(path, 'SqlServer', arm, {'a': (name, 'SqlServer', True, NAMESPACE, 'Accounts')})

                check(lines)
                for invalid in ([], [marker, window], lines + [marker], lines + [window], lines + [progress],
                                [marker.replace('SqlServer', 'PostgreSql'), window, progress],
                                [marker, window.replace('positive-start=20', 'positive-start=40'), progress],
                                [marker, window.replace('positive-end=30', 'positive-end=20000020'), progress],
                                [marker, window, progress.replace('fulfilled=True', 'fulfilled=False')],
                                [marker, window, progress.replace('safe=True', 'safe=False')],
                                [marker, window, progress.replace('cleanup-failed=False', 'cleanup-failed=True')],
                                [marker, window.replace('graph-ready=90', 'graph-ready=15').replace('graph-ready=-1', 'graph-ready=15'), progress]):
                    with self.assertRaises(ValueError):
                        check(invalid if any(line.startswith("history-graph") for line in invalid) else invalid + cleanup)
                with self.assertRaises(ValueError):
                    check(lines, passed=False)
                with self.assertRaises(ValueError):
                    check(lines, skipped=True)
                for invalid in (lines + ['history-graph cleanup-completed=True'],
                                [marker, window, progress, 'history-graph cleanup-completed=False']):
                    with self.assertRaises(ValueError):
                        check(invalid)
                if arm == 'overlap':
                    with self.assertRaises(ValueError):
                        check([marker, window, progress])

                check(lines)
                valid = path.read_text()
                for invalid in (valid.replace('executionId="execution"', 'executionId="other"', 1),
                                valid.replace('<Execution id="execution"/>', ''),
                                valid.replace(' warning="0"', ''),
                                valid.replace('testId="test"', 'testId="other"', 1)):
                    path.write_text(invalid)
                    with self.assertRaises(ValueError):
                        require_completion(path, 'SqlServer', arm, {'a': (TEST + '(provider: "SqlServer")',)})

    def test_Given_FirstArmFailure_When_ComparisonRuns_Then_SecondArmRunsOnceAndStaleReportsAreRemoved(self):
        """A failed first arm does not erase the independent second outcome or retain an earlier report."""
        with tempfile.TemporaryDirectory() as directory:
            results = Path(directory)
            for arm in ('isolated', 'overlap'):
                (results / f'{arm}.trx').write_text('stale')
            prepare_reports(results)
            self.assertFalse(list(results.glob('*.trx')))
            calls = []

            def invoke(arm, budget):
                """Fail one fixed arm and preserve the second arm's single attempt."""
                calls.append((arm, budget))
                if arm == 'isolated':
                    raise ValueError('synthetic')

            with self.assertRaises(RuntimeError):
                run_arms(results, invoke, lambda: 12)
            self.assertEqual([('isolated', 12), ('overlap', 12)], calls)
            self.assertEqual('isolated=failed\noverlap=passed\n', (results / 'outcomes.txt').read_text())
            calls.clear()

            def expired():
                """An exhausted deadline never invokes a test process."""
                raise RuntimeError('deadline')

            with self.assertRaises(RuntimeError):
                run_arms(results, invoke, expired)
            self.assertEqual([], calls)
            self.assertEqual('isolated=unattempted\noverlap=unattempted\n', (results / 'outcomes.txt').read_text())

    def test_Given_DiagnosticMode_When_OrdinaryRunnerStarts_Then_ExecutionIsRejectedBeforeDiscovery(self):
        """Even an explicitly empty mode is forbidden in ordinary acceptance."""
        for mode in ('', 'isolated', 'overlap'):
            result = subprocess.run([sys.executable, str(Path(__file__).with_name('verify.py')), 'PostgreSql'],
                                    env=os.environ | {MODE_VARIABLE: mode}, capture_output=True, text=True)
            self.assertNotEqual(0, result.returncode)
            self.assertIn('Ordinary acceptance cannot run with a history diagnostic mode', result.stderr)


if __name__ == '__main__':
    unittest.main()
