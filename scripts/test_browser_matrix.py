import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
from browser_matrix import select, changed_paths
from test_selection import catalogue, require_partition, require_execution_names
from check_coverage_inputs import require_inputs, browser_labels


class BrowserMatrixTests(unittest.TestCase):
    def require_cells(self, actual, browsers):
        expected = {(provider, browser, partition, story) for provider in ('PostgreSql', 'SqlServer') for browser in browsers for partition in ('standard', 'administrator')
                    for story in (('Accounts', 'Security') if partition == 'administrator' else ('',))}
        pairs = [(cell['provider'], cell['browser'], cell['partition'], cell['browser_story']) for cell in actual['include']]
        self.assertEqual(len(pairs), len(set(pairs)))
        self.assertEqual(expected, set(pairs))

    def test_chromium_always_covers_both_providers(self):
        self.require_cells(select('pull_request', ['docs/roadmap.md']), ['chromium'])

    def test_scheduled_manual_and_unknown_changes_cover_all_provider_partition_cells(self):
        for event, paths in [('schedule', []), ('workflow_dispatch', []), ('pull_request', None), ('push', None)]:
            with self.subTest(event=event):
                self.require_cells(select(event, paths), ['chromium', 'firefox', 'webkit'])

    def test_dependency_harness_and_trust_changes_cover_every_engine(self):
        for path in ['tests/é\nname.cs', 'global.json', 'NuGet.Config', 'dotnet-tools.json', 'Directory.Packages.props', 'a/packages.lock.json', '.github/workflows/verify.yml',
                     'tests/shared/IssuerMaterialFixture.cs', 'scripts/verify.py', 'eng/test-matrix.json',
                     'src/backend/IdentityPlatform.AuthServer/Features/Accounts/Shared/login.css']:
            with self.subTest(path=path):
                self.require_cells(select('pull_request', [path]), ['chromium', 'firefox', 'webkit'])

    def test_git_change_parser_preserves_newlines_and_unicode(self):
        paths = ['tests/é\nname.cs', 'docs/plain.md']
        with patch('browser_matrix.subprocess.check_output', return_value='\0'.join(paths) + '\0') as command:
            actual = changed_paths('before', 'after')
            self.assertEqual(paths, actual)
            self.assertIn('-z', command.call_args.args[0])
            self.require_cells(select('pull_request', actual), ['chromium', 'firefox', 'webkit'])

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
        """Independently enumerate all 55 obligations and reject omission or empty evidence for each."""
        matrix = select('schedule', [])
        labels = ['issuer-PostgreSql-flows', 'issuer-SqlServer-flows', 'issuer-PostgreSql-boundaries', 'issuer-SqlServer-boundaries', 'administrator-PostgreSql-recovery', 'administrator-SqlServer-recovery', 'administrator-PostgreSql-security', 'administrator-SqlServer-security', 'administrator-PostgreSql-accounts', 'administrator-SqlServer-accounts', 'administrator-PostgreSql-ceremonies', 'administrator-SqlServer-ceremonies', 'administrator-PostgreSql-bootstrap', 'administrator-SqlServer-bootstrap', 'administrator-PostgreSql-factors', 'administrator-SqlServer-factors', 'administrator-PostgreSql-governance-policy', 'administrator-PostgreSql-governance-commands', 'administrator-PostgreSql-governance-failures', 'administrator-SqlServer-governance-policy', 'administrator-SqlServer-governance-commands', 'administrator-SqlServer-governance-failures', 'contracts-core', 'contracts-chromium', 'contracts-firefox', 'contracts-webkit', 'unit', 'PostgreSql', 'SqlServer', 'PostgreSql-lifecycle-restart', 'SqlServer-lifecycle-restart',
                      'PostgreSql-lifecycle-distribution', 'SqlServer-lifecycle-distribution',
              'PostgreSql-lifecycle-keyrotation-activation', 'SqlServer-lifecycle-keyrotation-activation',
              'PostgreSql-lifecycle-keyrotation-retirement', 'SqlServer-lifecycle-keyrotation-retirement'] + [cell['label'] for cell in matrix['include']]
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            for label in labels:
                path = root / ('foundation-' + label) / 'coverage' / (label + '.cobertura.xml')
                path.parent.mkdir(parents=True)
                path.write_text('<coverage/>')
            self.assertEqual(55, require_inputs(root, matrix))
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

    def test_coverage_requires_every_provider_partition_and_exact_label(self):
        import copy
        matrix = select('workflow_dispatch', [])
        self.assertEqual(18, len(browser_labels(matrix)))
        for change in ('missing', 'relabel', 'duplicate', 'unknown', 'partial-engine', 'extra-field'):
            with self.subTest(change=change):
                invalid = copy.deepcopy(matrix)
                if change == 'missing': invalid['include'] = [cell for cell in invalid['include'] if cell['partition'] != 'administrator']
                elif change == 'relabel': invalid['include'][0]['label'] = 'unrelated-report'
                elif change == 'duplicate': invalid['include'][-1] = invalid['include'][0]
                elif change == 'unknown': invalid['include'][0]['provider'] = 'Other'
                elif change == 'partial-engine': invalid['include'] = [cell for cell in invalid['include'] if cell['browser'] != 'webkit']
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
