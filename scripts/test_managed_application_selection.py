"""Reject omissions and cross-story substitutions in bounded managed-application execution."""
import copy
import json
import re
from pathlib import Path
import subprocess
import sys
import unittest

from administrator_inventory import load_inventory, require_native_inventory
from administrator_group_selection import administrator_execution_invocations, group_invocations, group_filter, require_group_selections
from test_selection import catalogue

ROOT = Path(__file__).resolve().parents[1]
STORY = 'ManagedApplications'
NAMESPACE = 'IdentityPlatform.Administrator.Tests.Features.' + STORY


class ManagedApplicationSelectionTests(unittest.TestCase):
    def setUp(self):
        self.matrix = json.loads((ROOT / 'eng/test-matrix.json').read_text())
        self.inventory = load_inventory(ROOT / 'eng/administrator-test-matrix.json')
        self.raw = {'schemaVersion': 1, 'tests': [dict(uid=str(index), displayName=name, **record)
                    for index, (name, record) in enumerate(self.inventory['metadata'].items())]}
        self.catalogue = catalogue(json.dumps(self.raw), project='Administrator')
        self.groups = {provider: {group: {uid: test for uid, test in self.catalogue.items()
            if test[1] == provider and test[3] == NAMESPACE and
            self.inventory['metadata'][test[0]]['type']['typeName'] in item['classes']}
            for group, item in self.matrix['managed_application_groups'].items()}
            for provider in ('PostgreSql', 'SqlServer')}

    def verify(self, provider='PostgreSql'):
        require_native_inventory(json.dumps(self.raw), self.inventory, complete=True)
        require_group_selections(self.catalogue, self.groups[provider], provider, STORY, self.matrix, self.inventory)

    def test_Given_DefaultOrExplicitStory_When_Grouped_Then_EveryPriorIdentityRunsExactlyOnce(self):
        self.assertEqual(2933, len(self.catalogue))
        for provider, total in (('PostgreSql', 1338), ('SqlServer', 1443)):
            self.verify(provider)
            expected = [(STORY, 'Admission', 57), (STORY, 'Issuance', 66)]
            self.assertEqual(expected, administrator_execution_invocations(self.matrix, provider, STORY))
            all_stories = administrator_execution_invocations(self.matrix, provider)
            self.assertEqual(expected, [row for row in all_stories if row[0] == STORY])
            self.assertEqual(total, sum(row[2] for row in all_stories))
            self.assertEqual(set(self.matrix['administrator_stories']), {row[0] for row in all_stories})
            self.assertFalse(self.groups[provider]['Admission'].keys() & self.groups[provider]['Issuance'].keys())
            for group, count in (('Admission', 57), ('Issuance', 66)):
                self.assertEqual([(STORY, group, count)], administrator_execution_invocations(self.matrix, provider, STORY, managed_group=group))
                self.assertEqual([(group, count)], group_invocations(self.matrix, provider, STORY, group))
        self.assertEqual(23, len(self.matrix['managed_application_groups']['Admission']['classes']))
        self.assertEqual(17, len(self.matrix['managed_application_groups']['Issuance']['classes']))

    def test_Given_ManifestDrift_When_Grouped_Then_MissingOverlapSubstitutionAndRedistributionFail(self):
        for change in ('missing', 'extra', 'empty', 'overlap', 'cross-story', 'injection', 'provider', 'bool', 'redistribution'):
            with self.subTest(change=change):
                self.setUp()
                groups = self.matrix['managed_application_groups']
                admission = groups['Admission']
                if change == 'missing': del groups['Issuance']
                elif change == 'extra': groups['Unknown'] = copy.deepcopy(admission)
                elif change == 'empty': admission['classes'] = []
                elif change == 'overlap': admission['classes'][0] = groups['Issuance']['classes'][0]
                elif change == 'cross-story': admission['classes'][0] = self.matrix['browser_session_groups']['Product']['classes'][0]
                elif change == 'injection': admission['classes'][0] = 'Tests|Provider=SqlServer'
                elif change == 'provider': del admission['counts']['SqlServer']
                elif change == 'bool': admission['counts']['SqlServer'] = True
                elif change == 'redistribution':
                    admission['counts']['SqlServer'] += 1
                    groups['Issuance']['counts']['SqlServer'] -= 1
                with self.assertRaises(ValueError): self.verify()

    def test_Given_NativeFilterDrift_When_Compared_Then_ExactUidMetadataAndCompleteUnionAreRequired(self):
        for change in ('omit', 'overlap', 'provider', 'story', 'uid', 'tuple', 'missing-group', 'extra-group'):
            with self.subTest(change=change):
                self.setUp()
                groups = self.groups['PostgreSql']
                selected = groups['Admission']
                uid = next(iter(selected))
                if change == 'omit': del selected[uid]
                elif change == 'overlap': groups['Issuance'][uid] = selected[uid]
                elif change in ('provider', 'story'):
                    del selected[uid]
                    other = next(iter(self.groups['SqlServer']['Admission'])) if change == 'provider' else next(key for key, test in self.catalogue.items() if test[3].endswith('.BrowserSessions'))
                    selected[other] = self.catalogue[other]
                elif change == 'uid': selected['unreviewed'] = selected.pop(uid)
                elif change == 'tuple': selected[uid] = (*selected[uid][:2], True, *selected[uid][3:])
                elif change == 'missing-group': del groups['Issuance']
                elif change == 'extra-group': groups['Unknown'] = {}
                with self.assertRaises(ValueError): self.verify()

    def test_Given_TypeOrDisplayDrift_When_Discovered_Then_MetadataCannotRedefineTheStory(self):
        for change in ('type', 'display', 'missing'):
            self.setUp()
            row = next(row for row in self.raw['tests'] if '.ManagedApplications.' in row['displayName'])
            if change == 'type': row['type'] = dict(row['type'], typeName='UnreviewedTests')
            elif change == 'display': row['displayName'] += 'changed'
            else: self.raw['tests'].remove(row)
            with self.assertRaises(ValueError): self.verify()

    def test_Given_ReviewedClasses_When_Filtered_Then_OnlyPositiveExactClassClausesAreUsed(self):
        for provider in ('PostgreSql', 'SqlServer'):
            for group in ('Admission', 'Issuance'):
                expression = group_filter(self.matrix, provider, STORY, group)
                self.assertTrue(expression.startswith('Provider=' + provider + '&Category!=Browser&('))
                self.assertEqual(['FullyQualifiedName~' + NAMESPACE + '.' + name + '.' for name in sorted(self.matrix['managed_application_groups'][group]['classes'])], expression.split('&(', 1)[1][:-1].split('|'))
        for provider, story, group in (('Other', STORY, 'Admission'), ('PostgreSql', 'Applications', 'Admission'), ('PostgreSql', STORY, 'Product')):
            with self.assertRaises(ValueError): group_filter(self.matrix, provider, story, group)
        with self.assertRaises(ValueError): administrator_execution_invocations(self.matrix, 'PostgreSql', 'BrowserSessions', managed_group='Admission')

    def test_Given_IncompatibleCli_When_ManagedGroupRequested_Then_NoExecutionStarts(self):
        cases = (['unit', '--partition', 'administrator', '--story', STORY],
                 ['PostgreSql', '--partition', 'administrator'],
                 ['PostgreSql', '--partition', 'administrator', '--story', 'BrowserSessions'],
                 ['SqlServer', '--partition', 'issuer'],
                 ['SqlServer', '--partition', 'administrator', '--story', STORY, '--browser', 'chromium'],
                 ['SqlServer', '--partition', 'administrator', '--story', STORY, '--browser-session-group', 'Product'])
        for arguments in cases:
            result = subprocess.run([sys.executable, str(ROOT / 'scripts/verify.py'), *arguments, '--managed-application-group', 'Admission'], capture_output=True, text=True, timeout=10)
            self.assertEqual(2, result.returncode)
            self.assertIn('error:', result.stderr)

    def test_Given_HostedWorkflow_When_Reviewed_Then_BothProvidersRequireBothExplicitGroups(self):
        workflow = (ROOT / '.github/workflows/verify.yml').read_text()
        rows = re.findall(r'- \{ profile: (\w+), partition: administrator, story: ManagedApplications, managed_application_group: (\w+), label: ([\w-]+) \}', workflow)
        expected = [(provider, group, 'administrator-' + provider + '-managedapplications-' + group.lower())
                    for provider in ('PostgreSql', 'SqlServer') for group in ('Admission', 'Issuance')]
        self.assertEqual(expected, rows)
        self.assertEqual(4, workflow.count('story: ManagedApplications,'))
        self.assertIn("format('--managed-application-group {0}', matrix.managed_application_group)", workflow)
        self.assertIn('timeout=180 if args.profile == "unit" else 660', (ROOT / 'scripts/verify.py').read_text())


if __name__ == '__main__':
    unittest.main()
