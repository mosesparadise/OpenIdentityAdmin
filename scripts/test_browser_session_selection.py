"""Seed selection defects without executing a database or relaxing acceptance bounds."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import unittest

from administrator_inventory import load_inventory, require_native_inventory
from browser_session_selection import administrator_execution_invocations, browser_session_invocations, browser_session_filter, require_browser_session_selections
from test_selection import catalogue

ROOT = Path(__file__).resolve().parents[1]


class BrowserSessionSelectionTests(unittest.TestCase):
    def setUp(self):
        self.matrix = json.loads((ROOT / 'eng/test-matrix.json').read_text())
        self.inventory = load_inventory(ROOT / 'eng/administrator-test-matrix.json')
        self.raw = {'schemaVersion': 1, 'tests': [dict(uid=str(index), displayName=name, **record)
                    for index, (name, record) in enumerate(self.inventory['metadata'].items())]}
        self.catalogue = catalogue(json.dumps(self.raw), project='Administrator')
        self.groups = {}
        for provider in ('PostgreSql', 'SqlServer'):
            self.groups[provider] = {group: {uid: test for uid, test in self.catalogue.items()
                if test[1] == provider and test[3].endswith('.BrowserSessions') and
                self.inventory['metadata'][test[0]]['type']['typeName'] in item['classes']}
                for group, item in self.matrix['browser_session_groups'].items()}

    def verify(self, provider='PostgreSql'):
        require_native_inventory(json.dumps(self.raw), self.inventory, complete=True)
        require_browser_session_selections(self.catalogue, self.groups[provider], provider, self.matrix, self.inventory)

    def test_Given_DefaultAndExplicitGroups_When_Selected_Then_BothProvidersRetainEveryIdentityExactlyOnce(self):
        self.assertEqual(1851, len(self.catalogue))
        for provider in ('PostgreSql', 'SqlServer'):
            self.verify(provider)
            self.assertEqual([('Foundations', 158), ('Product', 96), ('Lifecycle', 51)], browser_session_invocations(self.matrix, provider))
            for group, count in (('Foundations', 158), ('Product', 96), ('Lifecycle', 51)):
                self.assertEqual([(group, count)], browser_session_invocations(self.matrix, provider, group))
            self.assertEqual(305, sum(map(len, self.groups[provider].values())))
            groups = list(self.groups[provider].values())
            for index, group in enumerate(groups):
                for other in groups[index + 1:]:
                    self.assertFalse(group.keys() & other.keys())
        self.assertEqual(37, len(self.matrix['browser_session_groups']['Foundations']['classes']))
        self.assertEqual(31, len(self.matrix['browser_session_groups']['Product']['classes']))
        self.assertEqual(17, len(self.matrix['browser_session_groups']['Lifecycle']['classes']))

    def test_Given_UngroupedInvocation_When_Expanded_Then_AllGroupsAreExecutedAndNoStoryDisappears(self):
        for provider, total in (('PostgreSql', 860), ('SqlServer', 965)):
            expected = [('BrowserSessions', 'Foundations', 158), ('BrowserSessions', 'Product', 96), ('BrowserSessions', 'Lifecycle', 51)]
            self.assertEqual(expected, administrator_execution_invocations(self.matrix, provider, 'BrowserSessions'))
            all_stories = administrator_execution_invocations(self.matrix, provider)
            self.assertEqual(expected, [item for item in all_stories if item[0] == 'BrowserSessions'])
            self.assertEqual(total, sum(item[2] for item in all_stories))
            self.assertEqual(set(self.matrix['administrator_stories']), {item[0] for item in all_stories})
            self.assertEqual([expected[1]], administrator_execution_invocations(self.matrix, provider, 'BrowserSessions', 'Product'))
            with self.assertRaises(ValueError): administrator_execution_invocations(self.matrix, provider, 'ProtocolAdmission', 'Product')

    def test_Given_ManifestDrift_When_Validated_Then_OmissionsOverlapsUnknownClassesAndCountsFail(self):
        for change in ('missing-group', 'extra-group', 'empty', 'duplicate', 'overlap', 'unknown', 'injection',
                       'missing-provider', 'zero', 'bool', 'count', 'redistribution'):
            with self.subTest(change=change):
                self.setUp()
                groups = self.matrix['browser_session_groups']
                product = groups['Product']
                if change == 'missing-group': del groups['Foundations']
                elif change == 'extra-group': groups['Unknown'] = copy.deepcopy(product)
                elif change == 'empty': product['classes'] = []
                elif change == 'duplicate': product['classes'].append(product['classes'][0])
                elif change == 'overlap': product['classes'][0] = groups['Foundations']['classes'][0]
                elif change == 'unknown': product['classes'][0] = 'UnreviewedTests'
                elif change == 'injection': product['classes'][0] = 'Tests|Provider=SqlServer'
                elif change == 'missing-provider': del product['counts']['SqlServer']
                elif change == 'zero': product['counts']['SqlServer'] = 0
                elif change == 'bool': product['counts']['SqlServer'] = True
                elif change == 'count': product['counts']['SqlServer'] += 1
                elif change == 'redistribution':
                    product['counts']['SqlServer'] += 1
                    groups['Foundations']['counts']['SqlServer'] -= 1
                with self.assertRaises(ValueError): self.verify()

    def test_Given_NativeFilterDrift_When_GroupsAreCompared_Then_ExactTuplesAndDisjointUnionAreRequired(self):
        for change in ('omit', 'substitute', 'provider', 'browser', 'overlap', 'missing-group', 'extra-group', 'uid', 'tuple'):
            with self.subTest(change=change):
                self.setUp()
                groups = self.groups['PostgreSql']
                product = groups['Product']
                uid = next(iter(product))
                if change == 'omit': del product[uid]
                elif change in ('substitute', 'provider', 'browser'):
                    del product[uid]
                    other = (next(iter(groups['Foundations'])) if change == 'substitute' else
                             next(iter(self.groups['SqlServer']['Product'])) if change == 'provider' else
                             next(key for key, value in self.catalogue.items() if value[2]))
                    product[other] = self.catalogue[other]
                elif change == 'overlap': groups['Foundations'][uid] = product[uid]
                elif change == 'missing-group': del groups['Foundations']
                elif change == 'extra-group': groups['Unknown'] = {}
                elif change == 'uid': product['unreviewed-uid'] = product.pop(uid)
                elif change == 'tuple': product[uid] = (*product[uid][:2], True, *product[uid][3:])
                with self.assertRaises(ValueError): self.verify()

    def test_Given_NativeTypeOrDisplayDrift_When_Discovered_Then_MetadataCannotSubstituteIdentity(self):
        for change in ('type', 'display', 'missing'):
            self.setUp()
            row = next(row for row in self.raw['tests'] if '.BrowserSessions.' in row['displayName'])
            if change == 'type': row['type'] = dict(row['type'], typeName='UnreviewedTests')
            elif change == 'display': row['displayName'] += 'changed'
            else: self.raw['tests'].remove(row)
            with self.assertRaises(ValueError): self.verify()

    def test_Given_ReviewedClasses_When_Filtered_Then_EachProviderHasPositiveExactClassClauses(self):
        for provider in ('PostgreSql', 'SqlServer'):
            for group in ('Foundations', 'Product', 'Lifecycle'):
                expression = browser_session_filter(self.matrix, provider, group)
                self.assertTrue(expression.startswith('Provider=' + provider + '&Category!=Browser&('))
                actual = expression.split('&(', 1)[1][:-1].split('|')
                expected = ['FullyQualifiedName~IdentityPlatform.Administrator.Tests.Features.BrowserSessions.' + name + '.'
                            for name in sorted(self.matrix['browser_session_groups'][group]['classes'])]
                self.assertEqual(expected, actual)
        for provider, group in (('Other', 'Product'), ('PostgreSql', 'Other')):
            with self.assertRaises(ValueError): browser_session_filter(self.matrix, provider, group)

    def test_Given_IncompatibleCli_When_GroupRequested_Then_ExecutionNeverStarts(self):
        cases = (['unit', '--partition', 'administrator', '--story', 'BrowserSessions'],
                 ['PostgreSql', '--partition', 'administrator'],
                 ['PostgreSql', '--partition', 'administrator', '--story', 'ProtocolAdmission'],
                 ['SqlServer', '--partition', 'issuer'],
                 ['SqlServer', '--partition', 'administrator', '--story', 'BrowserSessions', '--browser', 'chromium'])
        for arguments in cases:
            result = subprocess.run([sys.executable, str(ROOT / 'scripts/verify.py'), *arguments,
                                     '--browser-session-group', 'Product'], capture_output=True, text=True, timeout=10)
            self.assertEqual(2, result.returncode)
            self.assertIn('error:', result.stderr)


if __name__ == '__main__':
    unittest.main()
