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
    def test_Given_InitialPhase_When_CliPlansJobs_Then_ContractsAndDeferralAreExplicit(self):
        """Exercise workflow outputs, retaining all active contract engines and visible nonacceptance."""
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            event, output, summary = (root / name for name in ('event.json', 'output', 'summary'))
            event.write_text('{}')
            environment = dict(os.environ, GITHUB_EVENT_NAME='schedule', GITHUB_EVENT_PATH=str(event),
                               GITHUB_OUTPUT=str(output), GITHUB_STEP_SUMMARY=str(summary))
            result = subprocess.run([sys.executable, str(Path(__file__).with_name('browser_matrix.py'))],
                                    env=environment, capture_output=True, text=True, timeout=10)
            self.assertEqual(0, result.returncode, result.stderr)
            values = dict(line.split('=', 1) for line in output.read_text().splitlines())
            self.assertEqual({'partition': ['core', 'chromium', 'webkit']}, json.loads(values['contracts']))
            self.require_cells(json.loads(values['matrix']), ['chromium', 'webkit'])
            self.assertIn('Firefox deferred, not passed', summary.read_text())
            self.assertIn('A-009 internal roadmap development', summary.read_text())
            self.assertIn('before release acceptance', summary.read_text())
            workflow = Path(__file__).resolve().parents[1] / '.github/workflows/verify.yml'
            self.assertIn('matrix: ${{ fromJSON(needs.browser-plan.outputs.contracts) }}', workflow.read_text())

    def require_cells(self, actual, browsers):
        """Verify exact phase obligations independently of selector implementation constants."""
        expected = {(provider, browser, partition, story) for provider in ('PostgreSql', 'SqlServer') for browser in browsers for partition in ('standard', 'administrator')
                    for story in (('Accounts', 'Security') if partition == 'administrator' else ('',))}
        pairs = [(cell['provider'], cell['browser'], cell['partition'], cell['browser_story']) for cell in actual['include']]
        self.assertEqual(len(pairs), len(set(pairs)))
        self.assertEqual(expected, set(pairs))

    def test_Given_FullVerification_When_Selected_Then_BothEnginesCoverEveryProviderAndStory(self):
        """No change-path or event heuristic can silently remove WebKit during B2 work."""
        self.require_cells(select(), ['chromium', 'webkit'])

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
        """Independently enumerate all 56 current obligations and reject omission or empty evidence for each."""
        matrix = select()
        labels = ['administrator-PostgreSql-browsersessions-foundations', 'administrator-PostgreSql-browsersessions-product', 'administrator-PostgreSql-browsersessions-lifecycle', 'administrator-SqlServer-browsersessions-foundations', 'administrator-SqlServer-browsersessions-product', 'administrator-SqlServer-browsersessions-lifecycle', 'administrator-PostgreSql-protocoladmission', 'administrator-SqlServer-protocoladmission', 'issuer-PostgreSql-flows', 'issuer-SqlServer-flows', 'issuer-PostgreSql-boundaries', 'issuer-SqlServer-boundaries', 'administrator-PostgreSql-recovery', 'administrator-SqlServer-recovery', 'administrator-PostgreSql-security', 'administrator-SqlServer-security', 'administrator-PostgreSql-accounts', 'administrator-SqlServer-accounts', 'administrator-PostgreSql-ceremonies', 'administrator-SqlServer-ceremonies', 'administrator-PostgreSql-bootstrap', 'administrator-SqlServer-bootstrap', 'administrator-PostgreSql-factors', 'administrator-SqlServer-factors', 'administrator-PostgreSql-governance-policy', 'administrator-PostgreSql-governance-commands', 'administrator-PostgreSql-governance-failures', 'administrator-SqlServer-governance-policy', 'administrator-SqlServer-governance-commands', 'administrator-SqlServer-governance-failures', 'contracts-core', 'contracts-chromium', 'contracts-webkit', 'unit', 'PostgreSql', 'SqlServer', 'PostgreSql-lifecycle-restart', 'SqlServer-lifecycle-restart',
                      'PostgreSql-lifecycle-distribution', 'SqlServer-lifecycle-distribution',
              'PostgreSql-lifecycle-keyrotation-activation', 'SqlServer-lifecycle-keyrotation-activation',
              'PostgreSql-lifecycle-keyrotation-retirement', 'SqlServer-lifecycle-keyrotation-retirement'] + [cell['label'] for cell in matrix['include']]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for label in labels:
                path = root / ('foundation-' + label) / 'coverage' / (label + '.cobertura.xml')
                path.parent.mkdir(parents=True)
                path.write_text('<coverage/>')
            self.assertEqual(56, require_inputs(root, matrix))
            for label in labels:
                path = root / ('foundation-' + label) / 'coverage' / (label + '.cobertura.xml')
                path.unlink()
                with self.assertRaises(ValueError):
                    require_inputs(root, matrix)
                path.touch()
                with self.assertRaises(ValueError):
                    require_inputs(root, matrix)
                path.write_text('<coverage/>')
            with self.assertRaises(ValueError):
                require_inputs(root, {'include': matrix['include'] + [matrix['include'][0]]})

    def test_Given_PhaseMatrix_When_ObligationsAreAltered_Then_CoverageRejectsEvidence(self):
        """Verify exact phase obligations independently of selector implementation constants."""
        import copy
        matrix = select()
        self.assertEqual(12, len(browser_labels(matrix)))
        for change in ('missing', 'relabel', 'duplicate', 'unknown', 'partial-engine', 'extra-field', 'deferred-engine', 'chromium-only'):
            with self.subTest(change=change):
                invalid = copy.deepcopy(matrix)
                if change == 'missing': invalid['include'] = [cell for cell in invalid['include'] if cell['partition'] != 'administrator']
                elif change == 'relabel': invalid['include'][0]['label'] = 'unrelated-report'
                elif change == 'duplicate': invalid['include'][-1] = invalid['include'][0]
                elif change == 'unknown': invalid['include'][0]['provider'] = 'Other'
                elif change == 'partial-engine': invalid['include'] = [cell for cell in invalid['include'] if cell['browser'] != 'chromium']
                elif change == 'chromium-only': invalid['include'] = [cell for cell in invalid['include'] if cell['browser'] == 'chromium']
                elif change == 'deferred-engine': invalid['include'][0]['browser'] = 'firefox'
                elif change == 'extra-field': invalid['include'][0]['optional'] = True
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
