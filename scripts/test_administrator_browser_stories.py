"""Require actual discovery traits and exhaustive independently selected account-browser stories."""
import copy
import json
import unittest
from test_selection import catalogue, administrator_browser_invocations, require_administrator_browser, require_administrator_story
from test_administrator_selection import AdministratorBrowserSelectionTests


class DiscoveryTraitTests(unittest.TestCase):
    def raw(self, browser=True, stories=('Accounts',)):
        traits = [{'key': 'Provider', 'value': 'PostgreSql'}]
        if browser:
            traits.append({'key': 'Category', 'value': 'Browser'})
        traits += [{'key': 'BrowserStory', 'value': story} for story in stories]
        return json.dumps({'schemaVersion': 1, 'tests': [{'uid': 'native-id', 'displayName': 'native-case',
            'traits': traits, 'type': {'namespace': 'IdentityPlatform.Administrator.Tests.Features.' + ('Browser' if browser else 'Recovery')}}]})

    def test_real_discovery_accepts_each_reviewed_story_and_ordinary_untagged_cases(self):
        for story in ('Accounts', 'Security'):
            self.assertEqual(story, catalogue(self.raw(stories=(story,)), project='Administrator')['native-id'][4])
        self.assertIsNone(catalogue(self.raw(False, ()), project='Administrator')['native-id'][4])

    def test_real_discovery_rejects_missing_ambiguous_unknown_and_nonbrowser_traits(self):
        for browser, stories in [(True, ()), (True, ('Accounts', 'Accounts')), (True, ('Accounts', 'Security')),
                                 (True, ('Unknown',)), (False, ('Accounts',))]:
            with self.subTest(browser=browser, stories=stories), self.assertRaises(ValueError):
                catalogue(self.raw(browser, stories), project='Administrator')


class BrowserStoryControls(AdministratorBrowserSelectionTests):
    def test_Given_CompleteCatalogue_When_HttpStoryIsSelected_Then_BrowserStoryDriftIsRejected(self):
        """An HTTP invocation checks both providers' browser partitions before any test starts."""
        selected = {'PostgreSqlFactors': self.catalogue['PostgreSqlFactors']}
        require_administrator_story(self.catalogue, selected, 'PostgreSql', 'Factors', self.matrix, self.inventory)
        for provider in ('PostgreSql', 'SqlServer'):
            invalid = copy.deepcopy(self.catalogue)
            key = provider + 'Browser'
            invalid[key] = (*invalid[key][:4], 'Security')
            with self.subTest(provider=provider), self.assertRaisesRegex(ValueError, 'browser story count'):
                require_administrator_story(invalid, selected, 'PostgreSql', 'Factors', self.matrix, self.inventory)

    def test_Given_BrowserInventory_When_HttpStoryIsSelected_Then_InvalidStoryManifestIsRejected(self):
        """HTTP evidence cannot accept an absent or nonexhaustive browser-story manifest."""
        selected = {'PostgreSqlFactors': self.catalogue['PostgreSqlFactors']}
        for change in ('missing', 'unknown', 'zero', 'boolean', 'aggregate'):
            invalid = copy.deepcopy(self.matrix)
            stories = invalid['administrator_browser_stories']
            if change == 'missing': del invalid['administrator_browser_stories']
            elif change == 'unknown': stories['Unknown'] = 1
            elif change == 'zero': stories['Security'] = 0
            elif change == 'boolean': stories['Security'] = True
            else: stories['Security'] = 2
            with self.subTest(change=change), self.assertRaises(ValueError):
                require_administrator_story(self.catalogue, selected, 'PostgreSql', 'Factors', invalid, self.inventory)

    def test_both_selections_are_disjoint_and_exhaust_default_all(self):
        selections = []
        for story in ('Accounts', 'Security'):
            selected = {uid: row for uid, row in self.catalogue.items() if row[1] == 'PostgreSql' and row[2] and row[4] == story}
            require_administrator_browser(self.catalogue, selected, 'PostgreSql', self.matrix, self.inventory, story)
            selections.append(selected)
        self.assertFalse(set(selections[0]) & set(selections[1]))
        require_administrator_browser(self.catalogue, selections[0] | selections[1], 'PostgreSql', self.matrix, self.inventory)
        self.assertEqual([('Accounts', 1), ('Security', 1)], administrator_browser_invocations(self.matrix))
        self.assertEqual([('Security', 1)], administrator_browser_invocations(self.matrix, 'Security'))
        with self.assertRaises(ValueError):
            require_administrator_browser(self.catalogue, selections[0], 'PostgreSql', self.matrix, self.inventory, 'Security')

    def test_browser_story_inventory_rejects_missing_extra_invalid_and_balanced_drift(self):
        for mutation in ('missing', 'extra', 'zero', 'bool', 'aggregate', 'balanced-drift', 'unknown-selection'):
            matrix = copy.deepcopy(self.matrix)
            stories = matrix['administrator_browser_stories']
            if mutation == 'missing': del stories['Security']
            elif mutation == 'extra': stories['Unknown'] = 1
            elif mutation == 'zero': stories['Security'] = 0
            elif mutation == 'bool': stories['Security'] = True
            elif mutation == 'aggregate': matrix['browser']['Administrator'] = 3
            elif mutation == 'balanced-drift':
                stories['Security'] = 2
                matrix['browser']['Administrator'] = 3
            with self.subTest(mutation=mutation), self.assertRaises(ValueError):
                require_administrator_browser(self.catalogue, self.selected, 'PostgreSql', matrix, self.inventory,
                    'Unknown' if mutation == 'unknown-selection' else 'Accounts')


if __name__ == '__main__':
    unittest.main()
