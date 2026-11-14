"""Require the complete native protocol inventory across bounded admission/replay jobs."""
import copy
import json
from pathlib import Path
import re
import subprocess
import sys
import unittest

from administrator_inventory import load_inventory, require_native_inventory
from administrator_group_selection import administrator_execution_invocations, group_filter, require_group_selections
from test_selection import catalogue

ROOT = Path(__file__).resolve().parents[1]
STORY = 'ProtocolAdmission'
NAMESPACE = 'IdentityPlatform.Administrator.Tests.Features.' + STORY
PROVIDERS = ('PostgreSql', 'SqlServer')


class ProtocolAdmissionGroupTests(unittest.TestCase):
    def setUp(self):
        self.matrix = json.loads((ROOT / 'eng/test-matrix.json').read_text())
        self.inventory = load_inventory(ROOT / 'eng/administrator-test-matrix.json')
        self.raw = {'schemaVersion': 1, 'tests': [dict(uid=str(index), displayName=name, **record)
                    for index, (name, record) in enumerate(self.inventory['metadata'].items())]}
        self.native = catalogue(json.dumps(self.raw), project='Administrator')
        self.groups = {provider: {group: {uid: test for uid, test in self.native.items()
            if test[1] == provider and test[3] == NAMESPACE and
            self.inventory['metadata'][test[0]]['type']['typeName'] in item['classes']}
            for group, item in self.matrix['protocol_admission_groups'].items()} for provider in PROVIDERS}

    def verify(self, provider='PostgreSql'):
        require_native_inventory(json.dumps(self.raw), self.inventory, complete=True)
        require_group_selections(self.native, self.groups[provider], provider, STORY, self.matrix, self.inventory)

    def test_Given_DefaultOrExplicitStory_When_Grouped_Then_All162OriginalCasesRunOncePerProvider(self):
        for provider, total in (('PostgreSql', 1360), ('SqlServer', 1465)):
            self.verify(provider)
            expected = [(STORY, 'Admission', 107), (STORY, 'Replay', 55)]
            self.assertEqual(expected, administrator_execution_invocations(self.matrix, provider, STORY))
            all_stories = administrator_execution_invocations(self.matrix, provider)
            self.assertEqual(expected, [row for row in all_stories if row[0] == STORY])
            self.assertEqual(total, sum(row[2] for row in all_stories))
            for _, group, count in expected:
                self.assertEqual([(STORY, group, count)], administrator_execution_invocations(self.matrix, provider, STORY, protocol_group=group))
        self.assertEqual([27, 16], [len(self.matrix['protocol_admission_groups'][group]['classes']) for group in ('Admission', 'Replay')])

    def test_Given_ManifestDrift_When_Grouped_Then_OmissionsOverlapsAndRedistributedCountsFail(self):
        for change in ('missing-group', 'extra-group', 'missing-class', 'extra-class', 'overlap', 'story', 'provider', 'redistribution'):
            with self.subTest(change=change):
                self.setUp()
                groups = self.matrix['protocol_admission_groups']
                admission = groups['Admission']
                if change == 'missing-group': del groups['Replay']
                elif change == 'extra-group': groups['Other'] = copy.deepcopy(admission)
                elif change == 'missing-class': admission['classes'].pop()
                elif change == 'extra-class': admission['classes'].append('UnreviewedTests')
                elif change == 'overlap': admission['classes'][0] = groups['Replay']['classes'][0]
                elif change == 'story': admission['classes'][0] = 'ManagedJourneyTests'
                elif change == 'provider': del admission['counts']['SqlServer']
                else:
                    admission['counts']['SqlServer'] += 1
                    groups['Replay']['counts']['SqlServer'] -= 1
                with self.assertRaises(ValueError): self.verify()

    def test_Given_NativeFilterDrift_When_Compared_Then_ExactUidMetadataAndDisjointUnionAreRequired(self):
        for change in ('omit', 'overlap', 'provider', 'story', 'uid', 'tuple'):
            with self.subTest(change=change):
                self.setUp()
                selected = self.groups['PostgreSql']['Admission']
                uid = next(iter(selected))
                if change == 'omit': del selected[uid]
                elif change == 'overlap': self.groups['PostgreSql']['Replay'][uid] = selected[uid]
                elif change in ('provider', 'story'):
                    del selected[uid]
                    other = next(iter(self.groups['SqlServer']['Admission'])) if change == 'provider' else next(key for key, test in self.native.items() if test[3].endswith('.BrowserSessions'))
                    selected[other] = self.native[other]
                elif change == 'uid': selected['forged'] = selected.pop(uid)
                else: selected[uid] = (*selected[uid][:2], True, *selected[uid][3:])
                with self.assertRaises(ValueError): self.verify()
        self.setUp()
        row = next(row for row in self.raw['tests'] if '.ProtocolAdmission.' in row['displayName'])
        row['type'] = dict(row['type'], typeName='UnreviewedTests')
        with self.assertRaises(ValueError): self.verify()

    def test_Given_ReviewedClasses_When_Filtered_Then_OnlyPositiveExactClassClausesAreUsed(self):
        for provider in PROVIDERS:
            for group in ('Admission', 'Replay'):
                expression = group_filter(self.matrix, provider, STORY, group)
                self.assertTrue(expression.startswith('Provider=' + provider + '&Category!=Browser&('))
                self.assertEqual(['FullyQualifiedName~' + NAMESPACE + '.' + name + '.' for name in sorted(self.matrix['protocol_admission_groups'][group]['classes'])], expression.split('&(', 1)[1][:-1].split('|'))
        with self.assertRaises(ValueError): administrator_execution_invocations(self.matrix, 'PostgreSql', 'BrowserSessions', protocol_group='Admission')

    def test_Given_IncompatibleCli_When_ProtocolGroupRequested_Then_NoExecutionStarts(self):
        cases = (['unit', '--partition', 'administrator', '--story', STORY],
                 ['PostgreSql', '--partition', 'administrator'],
                 ['PostgreSql', '--partition', 'administrator', '--story', 'Applications'],
                 ['SqlServer', '--partition', 'issuer'],
                 ['SqlServer', '--partition', 'administrator', '--story', STORY, '--browser', 'chromium'],
                 ['SqlServer', '--partition', 'administrator', '--story', STORY, '--browser-session-group', 'Product'],
                 ['SqlServer', '--partition', 'administrator', '--story', STORY, '--managed-application-group', 'Admission'])
        for arguments in cases:
            result = subprocess.run([sys.executable, str(ROOT / 'scripts/verify.py'), *arguments, '--protocol-admission-group', 'Admission'], capture_output=True, text=True, timeout=10)
            self.assertEqual(2, result.returncode)
            self.assertIn('error:', result.stderr)

    def test_Given_HostedWorkflow_When_Reviewed_Then_BothProvidersRequireBothGroupsWithUnchangedDeadlines(self):
        workflow = (ROOT / '.github/workflows/verify.yml').read_text()
        rows = re.findall(r'- \{ profile: (\w+), partition: administrator, story: ProtocolAdmission, protocol_admission_group: (\w+), label: ([\w-]+) \}', workflow)
        self.assertEqual([(provider, group, 'administrator-' + provider + '-protocoladmission-' + group.lower()) for provider in PROVIDERS for group in ('Admission', 'Replay')], rows)
        self.assertEqual(4, workflow.count('story: ProtocolAdmission,'))
        self.assertIn("format('--protocol-admission-group {0}', matrix.protocol_admission_group)", workflow)
        self.assertIn('timeout=180 if args.profile == "unit" else 660', (ROOT / 'scripts/verify.py').read_text())


if __name__ == '__main__':
    unittest.main()
