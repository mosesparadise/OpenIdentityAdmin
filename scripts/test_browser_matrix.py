import copy
import json
import os
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from browser_matrix import select
from test_selection import catalogue, require_partition, require_execution_names
from check_coverage_inputs import require_inputs, browser_labels


class BrowserMatrixTests(unittest.TestCase):
    def test_Given_FullVerificationEvents_When_CliPlansJobs_Then_AllBrowserAndContractCellsAreRequired(self):
        """Exercise actual workflow outputs independently for every supported full-verification event."""
        for event_name in ('pull_request', 'push', 'schedule', 'workflow_dispatch'):
            with self.subTest(event=event_name), tempfile.TemporaryDirectory() as directory:
                root = Path(directory)
                event, output, summary = (root / name for name in ('event.json', 'output', 'summary'))
                event.write_text('{}')
                environment = dict(os.environ, GITHUB_EVENT_NAME=event_name, GITHUB_EVENT_PATH=str(event),
                                   GITHUB_OUTPUT=str(output), GITHUB_STEP_SUMMARY=str(summary))
                result = subprocess.run([sys.executable, str(Path(__file__).with_name('browser_matrix.py'))],
                                        env=environment, capture_output=True, text=True, timeout=10)
                self.assertEqual(0, result.returncode, result.stderr)
                values = dict(line.split('=', 1) for line in output.read_text().splitlines())
                self.assertEqual({'partition': ['core', 'chromium', 'firefox', 'webkit']}, json.loads(values['contracts']))
                self.assertEqual(self.expected_matrix(), json.loads(values['matrix']))
                for text in (summary.read_text(), result.stdout):
                    self.assertIn('Chromium, Firefox and WebKit required', text)
                    self.assertNotIn('Firefox deferred', text)
                    self.assertNotIn('A-007 initial development', text)

    @staticmethod
    def expected_matrix():
        """Enumerate the required cells without deriving the oracle from production phase policy."""
        return {'include': [{'provider': provider, 'browser': browser, 'partition': partition, 'browser_story': story,
                             'label': prefix + provider + '-' + browser + suffix}
                            for browser in ('chromium', 'firefox', 'webkit')
                            for provider in ('PostgreSql', 'SqlServer')
                            for partition, story, prefix, suffix in (('standard', '', '', ''),
                                ('administrator', 'Accounts', 'administrator-', '-accounts'),
                                ('administrator', 'Security', 'administrator-', '-security'))]}

    def test_Given_FullVerification_When_Selected_Then_ThreeEnginesCoverEveryProviderAndStory(self):
        """Reject a regression shared by production selection and its coverage consumer."""
        self.assertEqual(self.expected_matrix(), select())
        self.assertEqual(18, len({cell['label'] for cell in select()['include']}))

    def test_Given_RestoredFirefoxAccounts_When_WorkflowRuns_Then_HistoryArmsPrecedeOrdinaryEvidence(self):
        """Keep both generated matrices and the separate fail-closed history execution path wired."""
        workflow = (Path(__file__).resolve().parents[1] / '.github/workflows/verify.yml').read_text()
        self.assertIn('matrix: ${{ fromJSON(needs.browser-plan.outputs.contracts) }}', workflow)
        self.assertIn('matrix: ${{ fromJSON(needs.browser-plan.outputs.matrix) }}', workflow)
        browser_job = workflow[workflow.index('  browser:'):workflow.index('  contrast-experiment:')]
        self.assertIn("if: matrix.partition == 'administrator' && matrix.browser == 'firefox' && matrix.browser_story == 'Accounts'", browser_job)
        self.assertLess(browser_job.index('scripts/verify_history_experiment.py'), browser_job.index('scripts/verify.py'))
        self.assertIn('if: always()', browser_job)
        self.assertIn('artifacts/history-experiment/', browser_job)

    def test_partition_rejects_overlap_gap_wrong_category_and_equal_count_substitution(self):
        all_tests = {'a': ('HTTP', 'PostgreSql', False), 'b': ('Browser', 'PostgreSql', True), 'c': ('Other', 'SqlServer', True)}
        require_partition(all_tests, {'a': all_tests['a']}, {'b': all_tests['b']}, 'PostgreSql', 1, 1)
        for http, browser in [({'a': all_tests['a']}, {'a': all_tests['a']}), ({}, {'b': all_tests['b']}),
                              ({'a': all_tests['a']}, {'c': all_tests['c']}), ({'b': all_tests['b']}, {'a': all_tests['a']}),
                              ({'a': all_tests['a']}, {'b': ('Changed', 'PostgreSql', True)})]:
            with self.assertRaises(ValueError):
                require_partition(all_tests, http, browser, 'PostgreSql', 1, 1)

    def test_catalogue_rejects_missing_provider_duplicates_and_wrong_browser_tags(self):
        row = {'uid': 'a', 'displayName': 'A', 'traits': [{'key': 'Provider', 'value': 'PostgreSql'}], 'type': {'namespace': 'Protocol'}}
        self.assertEqual(1, len(catalogue(json.dumps({'schemaVersion': 1, 'tests': [row]}))))
        for rows in [[], [row, row], [dict(row, traits=[])], [dict(row, type={'namespace': 'IdentityPlatform.Issuer.Tests.Features.Browser'})]]:
            with self.assertRaises(ValueError):
                catalogue(json.dumps({'schemaVersion': 1, 'tests': rows}))
        with self.assertRaises(ValueError):
            catalogue('invalid')

    def test_Given_RequiredCoverageCells_When_AnyReportIsMissingEmptyOrDuplicated_Then_AcceptanceFails(self):
        """Independently enumerate all 57 restored obligations and reject omission or empty evidence for each."""
        matrix = self.expected_matrix()
        labels = ['administrator-PostgreSql-protocoladmission', 'administrator-SqlServer-protocoladmission', 'issuer-PostgreSql-flows', 'issuer-SqlServer-flows', 'issuer-PostgreSql-boundaries', 'issuer-SqlServer-boundaries', 'administrator-PostgreSql-recovery', 'administrator-SqlServer-recovery', 'administrator-PostgreSql-security', 'administrator-SqlServer-security', 'administrator-PostgreSql-accounts', 'administrator-SqlServer-accounts', 'administrator-PostgreSql-ceremonies', 'administrator-SqlServer-ceremonies', 'administrator-PostgreSql-bootstrap', 'administrator-SqlServer-bootstrap', 'administrator-PostgreSql-factors', 'administrator-SqlServer-factors', 'administrator-PostgreSql-governance-policy', 'administrator-PostgreSql-governance-commands', 'administrator-PostgreSql-governance-failures', 'administrator-SqlServer-governance-policy', 'administrator-SqlServer-governance-commands', 'administrator-SqlServer-governance-failures', 'contracts-core', 'contracts-chromium', 'contracts-firefox', 'contracts-webkit', 'unit', 'PostgreSql', 'SqlServer', 'PostgreSql-lifecycle-restart', 'SqlServer-lifecycle-restart',
                      'PostgreSql-lifecycle-distribution', 'SqlServer-lifecycle-distribution',
              'PostgreSql-lifecycle-keyrotation-activation', 'SqlServer-lifecycle-keyrotation-activation',
              'PostgreSql-lifecycle-keyrotation-retirement', 'SqlServer-lifecycle-keyrotation-retirement'] + [cell['label'] for cell in matrix['include']]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for label in labels:
                path = root / ('foundation-' + label) / 'coverage' / (label + '.cobertura.xml')
                path.parent.mkdir(parents=True)
                path.write_text('<coverage/>')
            self.assertEqual(57, len(set(labels)))
            self.assertEqual(57, require_inputs(root, matrix))
            for label in labels:
                path = root / ('foundation-' + label) / 'coverage' / (label + '.cobertura.xml')
                path.unlink()
                with self.assertRaises(ValueError):
                    require_inputs(root, matrix)
                path.touch()
                with self.assertRaises(ValueError):
                    require_inputs(root, matrix)
                path.write_text('<coverage/>')
                substituted = path.with_name('unrelated.cobertura.xml')
                path.rename(substituted)
                with self.assertRaises(ValueError):
                    require_inputs(root, matrix)
                substituted.rename(path)
            with self.assertRaises(ValueError):
                require_inputs(root, {'include': matrix['include'] + [matrix['include'][0]]})

    def test_Given_ThreeEngineMatrix_When_ObligationsAreAltered_Then_CoverageRejectsEvidence(self):
        """Reject omitted engines, old B2 scope, partial Firefox stories and equal-count substitutions."""
        matrix = self.expected_matrix()
        self.assertEqual(18, len(browser_labels(matrix)))
        for change in ('missing', 'relabel', 'duplicate', 'unknown-provider', 'unknown-engine', 'extra-field',
                       'missing-chromium', 'missing-firefox', 'missing-webkit', 'chromium-only', 'firefox-only',
                       'missing-firefox-provider', 'missing-firefox-story'):
            with self.subTest(change=change):
                invalid = copy.deepcopy(matrix)
                cells = invalid['include']
                if change == 'missing': invalid['include'] = [cell for cell in cells if cell['partition'] != 'administrator']
                elif change == 'relabel': cells[0]['label'] = 'unrelated-report'
                elif change == 'duplicate': cells[-1] = cells[0]
                elif change == 'unknown-provider': cells[0]['provider'] = 'Other'
                elif change == 'unknown-engine': cells[0]['browser'] = 'Other'
                elif change == 'extra-field': cells[0]['optional'] = True
                elif change in ('missing-chromium', 'missing-firefox', 'missing-webkit'):
                    invalid['include'] = [cell for cell in cells if cell['browser'] != change.removeprefix('missing-')]
                elif change in ('chromium-only', 'firefox-only'):
                    invalid['include'] = [cell for cell in cells if cell['browser'] == change.removesuffix('-only')]
                elif change == 'missing-firefox-provider':
                    invalid['include'] = [cell for cell in cells if not (cell['browser'] == 'firefox' and cell['provider'] == 'SqlServer')]
                elif change == 'missing-firefox-story':
                    invalid['include'] = [cell for cell in cells if not (cell['browser'] == 'firefox' and cell['browser_story'] == 'Accounts')]
                with self.assertRaises(ValueError): browser_labels(invalid)

    def test_execution_rejects_equal_count_wrong_identity_and_duplicates(self):
        with tempfile.TemporaryDirectory() as root:
            path = Path(root) / 'test.trx'
            selected = {'a': ('A', 'PostgreSql', True)}
            path.write_text('<TestRun><UnitTestResult testName="A"/></TestRun>')
            require_execution_names(path, selected)
            for xml in ['<TestRun/>', '<TestRun><UnitTestResult testName="B"/></TestRun>', '<TestRun><UnitTestResult testName="A"/><UnitTestResult testName="A"/></TestRun>']:
                path.write_text(xml)
                with self.assertRaises(ValueError):
                    require_execution_names(path, selected)


if __name__ == '__main__':
    unittest.main()
