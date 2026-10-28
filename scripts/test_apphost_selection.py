"""Seed omissions and substitutions against the three mandatory native AppHost groups."""
import copy
import json
from pathlib import Path
import unittest
import subprocess
import sys
from apphost_selection import apphost_catalogue, apphost_filter, apphost_invocations, require_apphost_selections
from control_runner import run

ROOT = Path(__file__).resolve().parents[1]
BASE = 'IdentityPlatform.AppHost.Tests.Features'


class AppHostSelectionTests(unittest.TestCase):
    """Use independent native-shaped identities, counts and namespace expectations."""

    def setUp(self):
        """Create eight foundation, ten BFF and three demo identities for each real provider."""
        self.matrix = json.loads((ROOT / 'eng/test-matrix.json').read_text())
        self.rows = {}
        for provider in ('PostgreSql', 'SqlServer'):
            self.rows[provider] = []
            for group, namespace, count in [('Foundation', BASE, 8), ('BffSessions', BASE + '.BffSessions', 10), ('LocalDemo', BASE + '.LocalDemo', 3)]:
                for index in range(count):
                    self.rows[provider].append({'uid': provider + group + str(index),
                        'displayName': f'{namespace}.FixtureTests.Case{index}({provider})',
                        'traits': [{'key': 'Provider', 'value': provider}],
                        'type': {'namespace': namespace, 'typeName': 'FixtureTests'}})

    def catalogue(self, provider):
        """Read synthetic discovery with the same schema as the native runner."""
        return apphost_catalogue(json.dumps({'schemaVersion': 1, 'tests': self.rows[provider]}))

    def selections(self, provider):
        """Derive independently expected namespace ownership from the fixture."""
        tests = self.catalogue(provider)
        return {'Foundation': {uid: test for uid, test in tests.items() if test[3] == BASE},
                'BffSessions': {uid: test for uid, test in tests.items() if test[3] == BASE + '.BffSessions'},
                'LocalDemo': {uid: test for uid, test in tests.items() if test[3] == BASE + '.LocalDemo'}}

    def test_Given_current_matrix_When_grouping_Then_all_twenty_one_cases_remain_mandatory(self):
        """Both providers retain eight-plus-ten-plus-three obligations including the browser deadline regressions."""
        for provider in self.rows:
            self.assertEqual([('Foundation', 8), ('BffSessions', 10), ('LocalDemo', 3)], apphost_invocations(self.matrix, provider))
            self.assertEqual(21, self.matrix['profiles'][provider]['AppHost'])
            require_apphost_selections(self.catalogue(provider), self.selections(provider), provider, self.matrix)

    def test_Given_manifest_drift_When_grouping_Then_missing_groups_and_invalid_counts_fail(self):
        """Reject missing/extra group, one-provider omission, zero, bool and changed total."""
        for change in ('missing', 'extra', 'provider', 'zero', 'bool', 'total'):
            matrix = copy.deepcopy(self.matrix)
            groups = matrix['apphost_native_groups']
            if change == 'missing': del groups['BffSessions']
            elif change == 'extra': groups['Other'] = groups['Foundation']
            elif change == 'provider': del groups['Foundation']['SqlServer']
            elif change == 'zero': groups['Foundation']['SqlServer'] = 0
            elif change == 'bool': groups['Foundation']['SqlServer'] = True
            else: groups['Foundation']['SqlServer'] += 1
            with self.subTest(change=change), self.assertRaises(ValueError):
                apphost_invocations(matrix, 'SqlServer')
        with self.assertRaises(ValueError): apphost_invocations(self.matrix, 'unit')

    def test_Given_metadata_spoofing_When_catalogued_Then_identity_and_namespace_must_agree(self):
        """Reject unreviewed namespace, display substitution, duplicate UID and provider omission."""
        for change in ('namespace', 'type', 'display', 'duplicate', 'provider', 'browser'):
            rows = copy.deepcopy(self.rows['SqlServer'])
            if change == 'namespace': rows[0]['type']['namespace'] = BASE + '.Unknown'
            elif change == 'type': rows[0]['type']['typeName'] = 'Type|Other'
            elif change == 'display': rows[0]['displayName'] = 'Other.Case'
            elif change == 'duplicate': rows.append(rows[0])
            elif change == 'provider': rows[0]['traits'] = []
            else: rows[0]['traits'].append({'key': 'Category', 'value': 'Browser'})
            with self.subTest(change=change), self.assertRaises(ValueError):
                apphost_catalogue(json.dumps({'schemaVersion': 1, 'tests': rows}))

    def test_Given_wrong_group_selection_When_validated_Then_omissions_overlap_and_substitution_fail(self):
        """Matching counts cannot hide a wrong identity, provider, group or changed metadata."""
        tests = self.catalogue('SqlServer')
        for change in ('missing-group', 'missing-case', 'overlap', 'provider', 'name', 'uid', 'empty'):
            groups = self.selections('SqlServer')
            uid = next(iter(groups['Foundation']))
            if change == 'missing-group': del groups['BffSessions']
            elif change == 'missing-case': del groups['Foundation'][uid]
            elif change == 'overlap': groups['BffSessions'][uid] = tests[uid]
            elif change == 'provider': groups = self.selections('PostgreSql')
            elif change == 'name': groups['Foundation'][uid] = ('changed', *tests[uid][1:])
            elif change == 'uid': groups['Foundation']['unknown'] = groups['Foundation'].pop(uid)
            else: groups['Foundation'] = {}
            with self.subTest(change=change), self.assertRaises(ValueError):
                require_apphost_selections(tests, groups, 'SqlServer', self.matrix)

    def test_Given_discovery_drift_When_validated_Then_wrong_provider_extra_or_missing_case_fails(self):
        """Selected subsets never excuse incomplete or contaminated complete discovery."""
        original = self.catalogue('SqlServer')
        uid = next(iter(original))
        for tests in ({k: v for k, v in original.items() if k != uid}, original | {'unknown': original[uid]},
                      self.catalogue('PostgreSql')):
            with self.assertRaises(ValueError):
                require_apphost_selections(tests, self.selections('SqlServer'), 'SqlServer', self.matrix)

    def test_Given_provider_groups_When_filtering_Then_nested_bff_namespace_is_excluded_from_foundation(self):
        """Native filter syntax explicitly separates nested namespaces without arbitrary arguments."""
        self.assertEqual(f'Provider=SqlServer&FullyQualifiedName~{BASE}.&FullyQualifiedName!~{BASE}.BffSessions.&FullyQualifiedName!~{BASE}.LocalDemo.', apphost_filter('SqlServer', 'Foundation'))
        self.assertEqual(f'Provider=PostgreSql&FullyQualifiedName~{BASE}.BffSessions.', apphost_filter('PostgreSql', 'BffSessions'))
        self.assertEqual(f'Provider=PostgreSql&FullyQualifiedName~{BASE}.LocalDemo.', apphost_filter('PostgreSql', 'LocalDemo'))
        for provider, group in [('unit', 'Foundation'), ('SqlServer', 'Other')]:
            with self.assertRaises(ValueError): apphost_filter(provider, group)

    def test_Given_IndependentAppHostJobs_When_Selected_Then_DefaultAndEveryGroupPreserveFullDiscovery(self):
        from test_selection import select_projects
        workflow = (ROOT / '.github/workflows/verify.yml').read_text()
        for provider in ('PostgreSql', 'SqlServer'):
            self.assertEqual({'Persistence': 12}, select_projects(self.matrix, provider, partition='foundation'))
            self.assertEqual({'AppHost': 21}, select_projects(self.matrix, provider, partition='apphost'))
            self.assertEqual([('Foundation', 8), ('BffSessions', 10), ('LocalDemo', 3)], apphost_invocations(self.matrix, provider))
            for group, count in (('Foundation', 8), ('BffSessions', 10), ('LocalDemo', 3)):
                self.assertEqual([(group, count)], apphost_invocations(self.matrix, provider, group))
                require_apphost_selections(self.catalogue(provider), self.selections(provider), provider, self.matrix)
                cell = '{ profile: ' + provider + ', partition: apphost, apphost_group: ' + group + ', label: ' + provider + '-apphost-' + group.lower() + ' }'
                self.assertIn(cell, workflow)
            with self.assertRaises(ValueError): apphost_invocations(self.matrix, provider, 'Unknown')
        source = (ROOT / 'scripts/verify.py').read_text()
        self.assertIn('for group, _ in apphost_invocations(matrix, args.profile)}', source)
        self.assertIn('require_apphost_selections(all_tests, groups, args.profile, matrix)', source)
        self.assertIn('timeout=180 if args.profile == "unit" else 660', source)

    def test_Given_IncompatibleAppHostSelection_When_Parsed_Then_NoTestExecutionStarts(self):
        cases = (['unit', '--partition', 'apphost'], ['SqlServer', '--partition', 'foundation'],
                 ['SqlServer', '--partition', 'apphost', '--browser', 'chromium'],
                 ['SqlServer', '--partition', 'apphost', '--story', 'Bootstrap'],
                 ['SqlServer', '--partition', 'apphost', '--issuer-group', 'Flows'],
                 ['SqlServer', '--partition', 'apphost', '--browser-story', 'BrowserLogin'],
                 ['SqlServer', '--partition', 'apphost', '--browser-session-group', 'Product'])
        for arguments in cases:
            result = subprocess.run([sys.executable, str(ROOT / 'scripts/verify.py'), *arguments, '--apphost-group', 'Foundation'],
                                    text=True, capture_output=True, timeout=10)
            self.assertEqual(2, result.returncode)
            self.assertIn('AppHost group requires', result.stderr)


if __name__ == '__main__':
    run(8)
