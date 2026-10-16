"""Seed omissions, overlap, substitutions and metadata drift against issuer execution groups."""
import copy
import json
from pathlib import Path
import unittest
from issuer_selection import (GROUPS, PROVIDERS, PROTOCOL_NAMESPACE, BROWSER_NAMESPACE,
                              issuer_catalogue, issuer_filter, issuer_invocations,
                              issuer_types, require_issuer_group, require_issuer_inventory, require_issuer_selections)
from test_selection import select_projects

ROOT = Path(__file__).resolve().parents[1]


class IssuerSelectionTests(unittest.TestCase):
    def setUp(self):
        """Build independently identified native-shaped cases covering every reviewed class and provider."""
        self.matrix = json.loads((ROOT / 'eng/test-matrix.json').read_text())
        self.rows = []
        for provider in PROVIDERS:
            for group, count in issuer_invocations(self.matrix, provider):
                classes = self.matrix['issuer_http_groups'][group]['classes']
                for index in range(count):
                    self.add_case(provider, classes[index % len(classes)], f'{group}{index}', False)
            for index in range(self.matrix['browser']['Issuer']):
                self.add_case(provider, 'LoginBrowserTests', f'Browser{index}', True)
        self.all_tests = issuer_catalogue(self.document())

    def add_case(self, provider, name, identity, browser):
        """Create a unique display/UID identity with native provider and browser metadata."""
        namespace = BROWSER_NAMESPACE if browser else PROTOCOL_NAMESPACE
        traits = [{'key': 'Provider', 'value': provider}]
        if browser:
            traits.append({'key': 'Category', 'value': 'Browser'})
        self.rows.append({'uid': provider + identity, 'displayName': f'{namespace}.{name}.{identity}({provider})',
                          'traits': traits, 'type': {'namespace': namespace, 'typeName': name}})

    def document(self, rows=None):
        """Serialize the native-shaped inventory without silently replacing an empty seed."""
        return json.dumps({'schemaVersion': 1, 'tests': self.rows if rows is None else rows})

    def selection(self, provider, group):
        """Construct the independent expected identity set from class ownership."""
        return {uid: test for uid, test in self.all_tests.items()
                if test[1] == provider and not test[2] and test[3] in issuer_types(self.matrix, group)}

    def test_Given_ReviewedGroups_When_Combined_Then_All292HttpCasesAppearExactlyOnce(self):
        """Preserve the reviewed 130/162 split and all 30 separately required browser cases per provider."""
        require_issuer_inventory(self.all_tests, self.matrix)
        for provider in PROVIDERS:
            self.assertEqual([('Flows', 130), ('Boundaries', 162)], issuer_invocations(self.matrix, provider))
            subsets = [self.selection(provider, group) for group in GROUPS]
            self.assertFalse(subsets[0].keys() & subsets[1].keys())
            expected = {uid: test for uid, test in self.all_tests.items() if test[1] == provider and not test[2]}
            self.assertEqual(expected, subsets[0] | subsets[1])
            self.assertEqual(292, len(expected))
            for group in GROUPS:
                require_issuer_group(self.all_tests, self.selection(provider, group), provider, group, self.matrix)

    def test_Given_SelectionDrift_When_Validated_Then_OmissionsSubstitutionsAndContaminationFail(self):
        """Equal counts cannot hide a wrong-provider/browser identity or a renamed case."""
        selected = self.selection('SqlServer', 'Flows')
        uid = next(iter(selected))
        browser = next((key, test) for key, test in self.all_tests.items() if test[2])
        missing = {key: test for key, test in selected.items() if key != uid}
        seeds = [{}, missing, missing | {browser[0]: browser[1]}, self.selection('PostgreSql', 'Flows'),
                 selected | {'unreviewed-uid': selected[uid]},
                 selected | {uid: ('substituted-name', *selected[uid][1:])},
                 self.selection('SqlServer', 'Boundaries')]
        for seed in seeds:
            with self.assertRaises(ValueError):
                require_issuer_group(self.all_tests, seed, 'SqlServer', 'Flows', self.matrix)

    def test_Given_ManifestDrift_When_Validated_Then_OverlapsMissingGroupsAndInvalidCountsFail(self):
        """Require reviewed group keys, disjoint class lists, exact provider shape and independent totals."""
        seeds = []
        for change in ('missing-group', 'unknown-group', 'duplicate-class', 'overlap', 'missing-provider',
                       'zero', 'boolean', 'wrong-total', 'empty-classes', 'filter-injection'):
            matrix = copy.deepcopy(self.matrix)
            groups = matrix['issuer_http_groups']
            flows = groups['Flows']
            if change == 'missing-group': del groups['Boundaries']
            elif change == 'unknown-group': groups['Other'] = groups.pop('Boundaries')
            elif change == 'duplicate-class': flows['classes'].append(flows['classes'][0])
            elif change == 'overlap': groups['Boundaries']['classes'].append(flows['classes'][0])
            elif change == 'missing-provider': del flows['counts']['SqlServer']
            elif change == 'zero': flows['counts']['SqlServer'] = 0
            elif change == 'boolean': flows['counts']['SqlServer'] = True
            elif change == 'wrong-total': flows['counts']['SqlServer'] += 1
            elif change == 'empty-classes': flows['classes'] = []
            else: flows['classes'][0] = 'Type|Category=Browser'
            seeds.append(matrix)
        for matrix in seeds:
            with self.assertRaises(ValueError):
                issuer_invocations(matrix, 'SqlServer')
        for provider, group in (('Other', 'Flows'), ('SqlServer', 'Other')):
            with self.assertRaises(ValueError): issuer_invocations(self.matrix, provider, group)

    def test_Given_CompleteCatalogueDrift_When_Validated_Then_MissingAndUnknownTypesFail(self):
        """Validate both providers and browser totals even when only one HTTP group is requested."""
        uid = next(iter(self.all_tests))
        test = self.all_tests[uid]
        browser_uid = next(key for key, value in self.all_tests.items() if value[2])
        seeds = [{key: value for key, value in self.all_tests.items() if key != uid},
                 {key: value for key, value in self.all_tests.items() if key != browser_uid},
                 self.all_tests | {uid: (*test[:3], PROTOCOL_NAMESPACE + '.UnknownTests')},
                 self.all_tests | {uid: (test[0], 'Other', *test[2:])}]
        for seed in seeds:
            with self.assertRaises(ValueError): require_issuer_inventory(seed, self.matrix)
        moved = copy.deepcopy(self.matrix)
        moved['issuer_http_groups']['Flows']['counts']['SqlServer'] -= 1
        moved['issuer_http_groups']['Boundaries']['counts']['SqlServer'] += 1
        with self.assertRaises(ValueError): require_issuer_inventory(self.all_tests, moved)

    def test_Given_NativeMetadataSpoofing_When_Catalogued_Then_IdentityAndTypeMustAgree(self):
        """Reject namespace/class/display-name spoofing before applying any group filter."""
        for change in ('namespace', 'type', 'display', 'duplicate'):
            rows = copy.deepcopy(self.rows)
            if change == 'namespace': rows[0]['type']['namespace'] = 'Unexpected.Namespace'
            elif change == 'type': rows[0]['type']['typeName'] = 'Type|Other'
            elif change == 'display': rows[0]['displayName'] = 'Unrelated.Case'
            else: rows.append(rows[0])
            with self.assertRaises(ValueError): issuer_catalogue(self.document(rows))

    def test_Given_ProjectPartitions_When_Selected_Then_FoundationAndIssuerAreDisjoint(self):
        """Preserve standard/all CLI meaning while CI can execute foundation and issuer groups independently."""
        for provider in PROVIDERS:
            foundation = select_projects(self.matrix, provider, partition='foundation')
            issuer = select_projects(self.matrix, provider, partition='issuer')
            self.assertEqual({'Persistence': 7, 'AppHost': 15}, foundation)
            self.assertEqual({'Issuer': 292}, issuer)
            self.assertFalse(foundation.keys() & issuer.keys())
            self.assertEqual(select_projects(self.matrix, provider, partition='standard'), foundation | issuer)
        for partition in ('foundation', 'issuer'):
            with self.assertRaises(ValueError): select_projects(self.matrix, 'unit', partition=partition)
            with self.assertRaises(ValueError): select_projects(self.matrix, 'SqlServer', 'webkit', partition)

    def test_Given_FixedFilters_When_Built_Then_OnlyReviewedClassesAndProviderAreSelected(self):
        """Keep class matches fully qualified and bounded before the method separator."""
        for provider in PROVIDERS:
            for group in GROUPS:
                expression = issuer_filter(self.matrix, provider, group)
                expected = '|'.join('FullyQualifiedName~' + name + '.' for name in sorted(issuer_types(self.matrix, group)))
                self.assertEqual(f'Provider={provider}&Category!=Browser&({expected})', expression)

    def test_Given_NativeGroupUnion_When_OneFilterDrifts_Then_TheWholePartitionFails(self):
        """An individually valid group cannot conceal a missing, overlapping or substituted peer group."""
        groups = {group: self.selection('SqlServer', group) for group in GROUPS}
        require_issuer_selections(self.all_tests, groups, 'SqlServer', self.matrix)
        seeds = [{}, {'Flows': groups['Flows']}, groups | {'Extra': {}},
                 groups | {'Boundaries': groups['Flows']}, groups | {'Flows': {}},
                 groups | {'Boundaries': groups['Boundaries'] | groups['Flows']}]
        for seed in seeds:
            with self.assertRaises(ValueError):
                require_issuer_selections(self.all_tests, seed, 'SqlServer', self.matrix)

    def test_Given_InvalidCliCombinations_When_Requested_Then_NoTestProcessStarts(self):
        """Reject unit/browser/story group misuse before attempting discovery or resource startup."""
        import subprocess
        import sys
        for args in (['unit', '--partition', 'issuer'],
                     ['SqlServer', '--issuer-group', 'Flows'],
                     ['SqlServer', '--partition', 'issuer', '--issuer-group', 'Flows', '--browser', 'webkit'],
                     ['SqlServer', '--partition', 'issuer', '--issuer-group', 'Flows', '--story', 'Restart']):
            result = subprocess.run([sys.executable, str(ROOT / 'scripts/verify.py'), *args],
                                    capture_output=True, text=True, timeout=10)
            self.assertEqual(2, result.returncode)
            self.assertNotIn('Running tests', result.stdout)


if __name__ == '__main__':
    unittest.main()
