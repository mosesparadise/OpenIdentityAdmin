"""Keep every existing browser-protection case and metadata under bounded disjoint invocations."""
import copy
import json
from pathlib import Path
import subprocess
import sys
import unittest
from administrator_group_selection import browser_execution_invocations, group_invocations, group_filter, require_group_selections
from administrator_inventory import load_inventory, require_native_inventory
from test_selection import catalogue

ROOT = Path(__file__).resolve().parents[1]
STORY = 'BrowserProtection'
GROUPS = ('Sessions', 'Applications')


class BrowserProtectionSelectionTests(unittest.TestCase):
    def setUp(self):
        """Use the existing portable inventory as realistic records, not a renamed or reduced test catalogue."""
        self.matrix = json.loads((ROOT/'eng/test-matrix.json').read_text())
        self.inventory = load_inventory(ROOT/'eng/administrator-test-matrix.json')
        self.raw = {'schemaVersion': 1, 'tests': [dict(uid=str(i), displayName=name, **record)
                    for i, (name, record) in enumerate(self.inventory['metadata'].items())]}
        self.tests = catalogue(json.dumps(self.raw), project='Administrator')

    def selections(self, provider):
        """Independently select native tuples by original provider/category/story and reviewed class."""
        result = {}
        for group in GROUPS:
            classes = self.matrix['browser_protection_groups'][group]['classes']
            result[group] = {uid: t for uid, t in self.tests.items() if t[1] == provider and t[2] and t[4] == STORY and
                             self.inventory['metadata'][t[0]]['type']['typeName'] in classes}
        return result

    def test_Given_OriginalStory_When_DefaultOrExplicitExecutionExpands_Then_BothGroupsAreMandatory(self):
        """Preserve all36 cases per provider, their old traits and the complete76-case browser inventory."""
        require_native_inventory(json.dumps(self.raw), self.inventory, complete=True)
        for provider in ('PostgreSql', 'SqlServer'):
            expected = [(STORY, 'Sessions', 15), (STORY, 'Applications', 21)]
            self.assertEqual(expected, browser_execution_invocations(self.matrix, provider, STORY))
            all_groups = browser_execution_invocations(self.matrix, provider)
            self.assertEqual(expected, [row for row in all_groups if row[0] == STORY])
            self.assertEqual(76, sum(row[2] for row in all_groups))
            selections = self.selections(provider)
            require_group_selections(self.tests, selections, provider, STORY, self.matrix, self.inventory)
            self.assertFalse(selections['Sessions'].keys() & selections['Applications'].keys())
            self.assertEqual(36, sum(map(len, selections.values())))
            for group, count in [('Sessions',15),('Applications',21)]:
                self.assertEqual([(STORY,group,count)], browser_execution_invocations(self.matrix, provider, STORY, group))
                expression = group_filter(self.matrix, provider, STORY, group)
                self.assertTrue(expression.startswith(f'Provider={provider}&Category=Browser&BrowserStory=BrowserProtection&('))
                self.assertNotIn('!=', expression)

    def test_Given_GroupReports_When_OmittedOverlappedOrSubstituted_Then_EqualCountsCannotHideDrift(self):
        """Reject missing groups, wrong-provider/story records and equal-count replacements before execution."""
        for change in ('missing','overlap','drop','substitute','provider','story','tuple'):
            selected = self.selections('PostgreSql')
            a = next(iter(selected['Applications'])); s = next(iter(selected['Sessions']))
            if change == 'missing': del selected['Applications']
            elif change == 'overlap': selected['Sessions'][a] = selected['Applications'][a]
            elif change == 'drop': del selected['Applications'][a]
            elif change == 'substitute':
                del selected['Applications'][a]; selected['Applications'][s] = selected['Sessions'][s]
            elif change in ('provider','story'):
                replacement = next((uid,t) for uid,t in self.tests.items() if t[2] and
                    (t[1] == 'SqlServer' if change == 'provider' else t[1] == 'PostgreSql' and t[4] != STORY))
                del selected['Applications'][a]; selected['Applications'][replacement[0]] = replacement[1]
            else: selected['Applications'][a] = (*selected['Applications'][a][:4], 'ApplicationEditor')
            with self.subTest(change=change), self.assertRaises(ValueError):
                require_group_selections(self.tests, selected, 'PostgreSql', STORY, self.matrix, self.inventory)

    def test_Given_ClassManifest_When_OverlapUnknownMissingOrCountsChange_Then_PartitionRejects(self):
        """Validate both providers and exact class coverage, including balanced count drift."""
        for change in ('duplicate','missing','unknown','zero','boolean','balanced','injection','extra-group'):
            matrix = copy.deepcopy(self.matrix); groups = matrix['browser_protection_groups']
            if change == 'duplicate': groups['Applications']['classes'].append(groups['Sessions']['classes'][0])
            elif change == 'missing': groups['Sessions']['classes'].pop()
            elif change == 'unknown': groups['Sessions']['classes'][0] = 'UnreviewedBrowserTests'
            elif change == 'zero': groups['Sessions']['counts']['SqlServer'] = 0
            elif change == 'boolean': groups['Sessions']['counts']['PostgreSql'] = True
            elif change == 'balanced':
                groups['Sessions']['counts']['SqlServer'] += 1; groups['Applications']['counts']['SqlServer'] -= 1
            elif change == 'injection': groups['Sessions']['classes'][0] = 'X|Category=Browser'
            else: groups['Unknown'] = copy.deepcopy(groups['Sessions'])
            with self.subTest(change=change), self.assertRaises(ValueError):
                require_group_selections(self.tests, self.selections('PostgreSql'), 'PostgreSql', STORY, matrix, self.inventory)

    def test_Given_NativeMetadata_When_AnyTraitOrTypeChanges_Then_ExistingInventoryStillRejects(self):
        """Execution grouping does not relax native complete-metadata validation."""
        for change in ('trait','type','missing'):
            raw = copy.deepcopy(self.raw)
            row = next(x for x in raw['tests'] if {'key':'BrowserStory','value':STORY} in x['traits'])
            if change == 'trait': row['traits'].append({'key':'Unreviewed','value':'true'})
            elif change == 'type': row['type']['typeName'] += 'Replacement'
            else: raw['tests'].remove(row)
            with self.subTest(change=change), self.assertRaises(ValueError):
                require_native_inventory(json.dumps(raw), self.inventory, complete=True)

    def test_Given_GroupArgument_When_StoryOrBrowserIsWrong_Then_CliRejectsBeforeRunningTools(self):
        """Invalid group combinations cannot silently select another partition or unbounded default."""
        for story, group in [('Security','Sessions'),(None,'Applications'),(STORY,'Unknown')]:
            with self.assertRaises(ValueError): browser_execution_invocations(self.matrix,'PostgreSql',story,group)
        for arguments in [[],['--browser','webkit'],['--partition','administrator','--browser','webkit','--browser-story','Security']]:
            result = subprocess.run([sys.executable,str(ROOT/'scripts/verify.py'),'PostgreSql',*arguments,'--browser-protection-group','Sessions'],
                                    cwd=ROOT,capture_output=True,text=True,timeout=10)
            self.assertEqual(2,result.returncode,result.stderr)
            self.assertIn('Browser-protection group requires',result.stderr)


if __name__ == '__main__':
    unittest.main()
