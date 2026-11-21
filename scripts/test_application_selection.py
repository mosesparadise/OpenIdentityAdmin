"""Reject omissions and substitutions across bounded registration and retirement execution."""
import copy
import json
from pathlib import Path
import re
import subprocess
import sys
import unittest

from administrator_inventory import load_inventory, require_native_inventory
from administrator_group_selection import administrator_execution_invocations, group_invocations, group_filter, require_group_selections
from test_selection import catalogue

ROOT = Path(__file__).resolve().parents[1]
STORY = 'Applications'
NAMESPACE = 'IdentityPlatform.Administrator.Tests.Features.Applications'
EXPECTED = [('Registration', 140), ('Retirement', 39)]


class ApplicationSelectionTests(unittest.TestCase):
    def setUp(self):
        """Construct complete committed native metadata and independently owned provider selections."""
        self.matrix = json.loads((ROOT / 'eng/test-matrix.json').read_text())
        self.inventory = load_inventory(ROOT / 'eng/administrator-test-matrix.json')
        self.raw = {'schemaVersion': 1, 'tests': [dict(uid=str(index), displayName=name, **record)
                    for index, (name, record) in enumerate(self.inventory['metadata'].items())]}
        self.catalogue = catalogue(json.dumps(self.raw), project='Administrator')
        self.groups = {provider: {group: {uid: test for uid, test in self.catalogue.items()
            if test[1] == provider and test[3] == NAMESPACE and
            self.inventory['metadata'][test[0]]['type']['typeName'] in item['classes']}
            for group, item in self.matrix['application_groups'].items()}
            for provider in ('PostgreSql', 'SqlServer')}

    def verify(self, provider='PostgreSql'):
        """Require portable metadata and an exact native union before accepting any group."""
        require_native_inventory(json.dumps(self.raw), self.inventory, complete=True)
        require_group_selections(self.catalogue, self.groups[provider], provider, STORY, self.matrix, self.inventory)

    def test_Given_DefaultAndExplicitStory_When_Grouped_Then_AllOldAndRetirementCasesRunExactlyOnce(self):
        """Check both default and explicit execution preserve every original and retirement identity."""
        for provider, total in (('PostgreSql', 1430), ('SqlServer', 1535)):
            self.verify(provider)
            expected = [(STORY, group, count) for group, count in EXPECTED]
            self.assertEqual(expected, administrator_execution_invocations(self.matrix, provider, STORY))
            all_stories = administrator_execution_invocations(self.matrix, provider)
            self.assertEqual(expected, [row for row in all_stories if row[0] == STORY])
            self.assertEqual(total, sum(row[2] for row in all_stories))
            self.assertFalse(self.groups[provider]['Registration'].keys() & self.groups[provider]['Retirement'].keys())
            for group, count in EXPECTED:
                self.assertEqual([(STORY, group, count)], administrator_execution_invocations(self.matrix, provider, STORY, application_group=group))
                self.assertEqual([(group, count)], group_invocations(self.matrix, provider, STORY, group))
                for test in self.groups[provider][group].values():
                    name = self.inventory['metadata'][test[0]]['type']['typeName']
                    self.assertEqual(group == 'Retirement', name.startswith('ApplicationRetirement'))
        self.assertEqual(34, len(self.matrix['application_groups']['Registration']['classes']))
        self.assertEqual(13, len(self.matrix['application_groups']['Retirement']['classes']))

    def test_Given_ManifestDrift_When_Validated_Then_MissingOverlappingAndSubstitutedClassesFail(self):
        """Seed independent manifest defects without changing actual native discovery."""
        for change in ('missing', 'extra', 'empty', 'overlap', 'foreign', 'injection', 'provider', 'bool', 'redistribution'):
            with self.subTest(change=change):
                self.setUp()
                groups = self.matrix['application_groups']
                item = groups['Retirement']
                if change == 'missing': del groups['Registration']
                elif change == 'extra': groups['Unknown'] = copy.deepcopy(item)
                elif change == 'empty': item['classes'] = []
                elif change == 'overlap': item['classes'][0] = groups['Registration']['classes'][0]
                elif change == 'foreign': item['classes'][0] = 'ManagedRetirementRaceTests'
                elif change == 'injection': item['classes'][0] = 'Tests|Provider=SqlServer'
                elif change == 'provider': del item['counts']['SqlServer']
                elif change == 'bool': item['counts']['SqlServer'] = True
                elif change == 'redistribution':
                    item['counts']['SqlServer'] += 1
                    groups['Registration']['counts']['SqlServer'] -= 1
                with self.assertRaises(ValueError): self.verify()

    def test_Given_NativeFilterDrift_When_Reconciled_Then_ExactIdentityMetadataAndDisjointUnionAreRequired(self):
        """Reject incomplete or substituted executed selections even when a count could appear plausible."""
        for change in ('omit', 'overlap', 'provider', 'uid', 'tuple', 'missing-group', 'extra-group'):
            with self.subTest(change=change):
                self.setUp()
                groups = self.groups['PostgreSql']
                selected = groups['Retirement']
                uid = next(iter(selected))
                if change == 'omit': del selected[uid]
                elif change == 'overlap': groups['Registration'][uid] = selected[uid]
                elif change == 'provider':
                    del selected[uid]
                    other = next(iter(self.groups['SqlServer']['Retirement']))
                    selected[other] = self.catalogue[other]
                elif change == 'uid': selected['substituted'] = selected.pop(uid)
                elif change == 'tuple': selected[uid] = (*selected[uid][:2], True, *selected[uid][3:])
                elif change == 'missing-group': del groups['Registration']
                elif change == 'extra-group': groups['Unknown'] = {}
                with self.assertRaises(ValueError): self.verify()

    def test_Given_ReviewedGroups_When_Filtered_Then_OnlyExactPositiveClassClausesSelectCases(self):
        """Bind filters to exact reviewed classes and reject another story borrowing the group option."""
        for provider in ('PostgreSql', 'SqlServer'):
            for group, _ in EXPECTED:
                expression = group_filter(self.matrix, provider, STORY, group)
                self.assertTrue(expression.startswith('Provider=' + provider + '&Category!=Browser&('))
                self.assertEqual(['FullyQualifiedName~' + NAMESPACE + '.' + name + '.'
                    for name in sorted(self.matrix['application_groups'][group]['classes'])], expression.split('&(', 1)[1][:-1].split('|'))
        with self.assertRaises(ValueError): administrator_execution_invocations(self.matrix, 'PostgreSql', 'ManagedApplications', application_group='Registration')

    def test_Given_IncompatibleCli_When_ApplicationGroupRequested_Then_NoExecutionStarts(self):
        """Prove incompatible requests fail in argument parsing before dotnet or provider execution."""
        cases = (['unit', '--partition', 'administrator', '--story', STORY],
                 ['PostgreSql', '--partition', 'administrator'],
                 ['PostgreSql', '--partition', 'administrator', '--story', 'ManagedApplications'],
                 ['SqlServer', '--partition', 'issuer'],
                 ['SqlServer', '--partition', 'administrator', '--story', STORY, '--browser', 'chromium'],
                 ['SqlServer', '--partition', 'administrator', '--story', STORY, '--managed-application-group', 'Admission'])
        for arguments in cases:
            result = subprocess.run([sys.executable, str(ROOT / 'scripts/verify.py'), *arguments, '--application-group', 'Registration'], capture_output=True, text=True, timeout=10)
            self.assertEqual(2, result.returncode)
            self.assertIn('error:', result.stderr)

    def test_Given_HostedWorkflow_When_Checked_Then_BothProvidersRequireBothBoundedGroups(self):
        """Require every hosted provider/group cell and the original execution deadline."""
        workflow = (ROOT / '.github/workflows/verify.yml').read_text()
        rows = re.findall(r'- \{ profile: (\w+), partition: administrator, story: Applications, application_group: (\w+), label: ([\w-]+) \}', workflow)
        self.assertEqual([(provider, group, 'administrator-' + provider + '-applications-' + group.lower())
            for provider in ('PostgreSql', 'SqlServer') for group, _ in EXPECTED], rows)
        self.assertEqual(4, workflow.count('story: Applications,'))
        self.assertIn("format('--application-group {0}', matrix.application_group)", workflow)
        self.assertIn('timeout=180 if args.profile == "unit" else 660', (ROOT / 'scripts/verify.py').read_text())


if __name__ == '__main__':
    unittest.main()
