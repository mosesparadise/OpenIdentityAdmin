#!/usr/bin/env python3
import unittest
from test_selection import require_administrator_partition, select_projects, administrator_invocations, require_administrator_story


class AdministratorSelectionTests(unittest.TestCase):
    def setUp(self):
        self.catalogue = {p: (p + '-case', p, False, 'IdentityPlatform.Administrator.Tests.Features.Bootstrap', None) for p in ('PostgreSql', 'SqlServer')}
        self.counts = {'PostgreSql': 1, 'SqlServer': 1}
        self.names = {p: [p + '-case'] for p in self.counts}
        self.selected = {'PostgreSql': self.catalogue['PostgreSql']}

    def verify(self):
        require_administrator_partition(self.catalogue, self.selected, 'PostgreSql', self.counts, self.names)

    def test_control(self):
        self.verify()

    def test_missing_substituted_duplicate_and_unreviewed_cases_fail(self):
        for mutation in ('missing', 'substituted', 'duplicate', 'unknown-namespace', 'wrong-provider', 'browser'):
            with self.subTest(mutation=mutation):
                self.setUp()
                if mutation == 'missing': self.catalogue.pop('SqlServer')
                elif mutation == 'substituted': self.selected = {'SqlServer': self.catalogue['SqlServer']}
                elif mutation == 'duplicate': self.names['SqlServer'].append('SqlServer-case')
                elif mutation == 'unknown-namespace': self.catalogue['SqlServer'] = ('SqlServer-case', 'SqlServer', False, 'Unexpected', None)
                elif mutation == 'wrong-provider': self.catalogue['SqlServer'] = ('SqlServer-case', 'Other', False, 'IdentityPlatform.Administrator.Tests.Features.Bootstrap', None)
                elif mutation == 'browser': self.catalogue['SqlServer'] = ('SqlServer-case', 'SqlServer', True, 'IdentityPlatform.Administrator.Tests.Features.Bootstrap', None)
                with self.assertRaises(ValueError): self.verify()

    def test_isolation_and_default_all(self):
        matrix = {'profiles': {'PostgreSql': {'Issuer': 2, 'Issuer.Lifecycle': 3, 'Administrator': 1}, 'unit': {'Core': 1}}, 'browser': {'Issuer': 4}}
        self.assertEqual({'Issuer': 2}, select_projects(matrix, 'PostgreSql', partition='standard'))
        self.assertEqual({'Administrator': 1}, select_projects(matrix, 'PostgreSql', partition='administrator'))
        self.assertEqual(matrix['profiles']['PostgreSql'], select_projects(matrix, 'PostgreSql'))
        for profile, browser in [('unit', None), ('PostgreSql', 'webkit')]:
            with self.assertRaises(ValueError): select_projects(matrix, profile, browser, 'administrator')


class AdministratorStoryTests(unittest.TestCase):
    def setUp(self):
        self.matrix = {'administrator_stories': {s: {p: 1 for p in ('PostgreSql', 'SqlServer')} for s in ('Bootstrap', 'Factors', 'Ceremonies', 'Accounts', 'Recovery', 'Security')},
                       'profiles': {p: {'Administrator': 6} for p in ('PostgreSql', 'SqlServer')}}
        self.catalogue = {p + s: (p + s, p, False, 'IdentityPlatform.Administrator.Tests.Features.' + s, None)
                          for p in ('PostgreSql', 'SqlServer') for s in ('Bootstrap', 'Factors', 'Ceremonies', 'Accounts', 'Recovery', 'Security')}
        self.inventory = {p: [p + s for s in ('Bootstrap', 'Factors', 'Ceremonies', 'Accounts', 'Recovery', 'Security')] for p in ('PostgreSql', 'SqlServer')}
        self.selected = {'PostgreSqlFactors': self.catalogue['PostgreSqlFactors']}

    def verify(self):
        require_administrator_story(self.catalogue, self.selected, 'PostgreSql', 'Factors', self.matrix, self.inventory)

    def test_control_and_invocation_isolation(self):
        self.verify()
        self.assertEqual([('Bootstrap', 1), ('Factors', 1), ('Ceremonies', 1), ('Accounts', 1), ('Recovery', 1), ('Security', 1)], administrator_invocations(self.matrix, 'PostgreSql'))
        self.assertEqual([('Factors', 1)], administrator_invocations(self.matrix, 'PostgreSql', 'Factors'))
        for story in ('Restart', 'Unknown'):
            with self.assertRaises(ValueError): administrator_invocations(self.matrix, 'PostgreSql', story)
        with self.assertRaises(ValueError): administrator_invocations(self.matrix, 'unit')

    def test_rejects_missing_extra_invalid_and_nonexhaustive_stories(self):
        for change in ('missing', 'extra', 'provider', 'zero', 'bool', 'aggregate', 'balanced-drift'):
            with self.subTest(change=change):
                self.setUp()
                stories = self.matrix['administrator_stories']
                if change == 'missing': del stories['Factors']
                elif change == 'extra': stories['Unknown'] = dict(stories['Factors'])
                elif change == 'provider': del stories['Factors']['SqlServer']
                elif change == 'zero': stories['Factors']['SqlServer'] = 0
                elif change == 'bool': stories['Factors']['SqlServer'] = True
                elif change == 'aggregate': self.matrix['profiles']['SqlServer']['Administrator'] = 5
                elif change == 'balanced-drift':
                    stories['Factors']['SqlServer'] = 2
                    stories['Bootstrap']['SqlServer'] = 2
                    self.matrix['profiles']['SqlServer']['Administrator'] = 8
                with self.assertRaises(ValueError): self.verify()

    def test_complete_inventory_and_exact_story_selection_remain_mandatory(self):
        for change in ('other-provider', 'other-story', 'duplicate', 'namespace', 'browser', 'substitute', 'omit', 'same-count-drift'):
            with self.subTest(change=change):
                self.setUp()
                if change == 'other-provider': del self.catalogue['SqlServerBootstrap']
                elif change == 'other-story': del self.catalogue['PostgreSqlBootstrap']
                elif change == 'duplicate': self.inventory['SqlServer'][0] = self.inventory['SqlServer'][1]
                elif change == 'namespace': self.catalogue['SqlServerFactors'] = ('SqlServerFactors', 'SqlServer', False, 'Unexpected', None)
                elif change == 'browser': self.catalogue['SqlServerFactors'] = ('SqlServerFactors', 'SqlServer', True, 'IdentityPlatform.Administrator.Tests.Features.Factors', None)
                elif change == 'substitute': self.selected = {'PostgreSqlBootstrap': self.catalogue['PostgreSqlBootstrap']}
                elif change == 'omit': self.selected = {}
                elif change == 'same-count-drift':
                    row = self.catalogue['SqlServerBootstrap']
                    self.catalogue['SqlServerBootstrap'] = (*row[:3], 'IdentityPlatform.Administrator.Tests.Features.Factors', None)
                with self.assertRaises(ValueError): self.verify()


class AdministratorBrowserSelectionTests(AdministratorStoryTests):
    def setUp(self):
        super().setUp()
        self.matrix['browser'] = {'Administrator': 2}
        self.matrix['administrator_browser_stories'] = {'Accounts': 1, 'Security': 1}
        for provider in ('PostgreSql', 'SqlServer'):
            name = provider + 'Browser'
            self.catalogue[name] = (name, provider, True, 'IdentityPlatform.Administrator.Tests.Features.Browser', 'Accounts')
            self.inventory[provider].append(name)
            security = provider + 'SecurityBrowser'
            self.catalogue[security] = (security, provider, True, 'IdentityPlatform.Administrator.Tests.Features.Browser', 'Security')
            self.inventory[provider].append(security)
        self.selected = {'PostgreSqlBrowser': self.catalogue['PostgreSqlBrowser']}

    def verify(self):
        from test_selection import require_administrator_browser
        require_administrator_browser(self.catalogue, self.selected, 'PostgreSql', self.matrix, self.inventory, 'Accounts')

    def test_control_and_invocation_isolation(self):
        self.verify()
        self.assertEqual({'Administrator': 2}, select_projects(self.matrix, 'PostgreSql', 'webkit', 'administrator'))

    def test_browser_inventory_selection_and_category_fail_closed(self):
        for change in ('missing', 'substitute', 'ordinary', 'provider', 'count', 'bool', 'category', 'namespace', 'story'):
            with self.subTest(change=change):
                self.setUp()
                if change == 'missing': del self.catalogue['SqlServerBrowser']
                elif change == 'substitute': self.inventory['SqlServer'][-1] = 'unreviewed-browser'
                elif change == 'ordinary': self.selected = {'PostgreSqlFactors': self.catalogue['PostgreSqlFactors']}
                elif change == 'provider': self.selected = {'SqlServerBrowser': self.catalogue['SqlServerBrowser']}
                elif change == 'count': self.matrix['browser']['Administrator'] = 3
                elif change == 'bool': self.matrix['browser']['Administrator'] = True
                elif change == 'category': self.catalogue['SqlServerBrowser'] = ('SqlServerBrowser', 'SqlServer', False, 'IdentityPlatform.Administrator.Tests.Features.Browser', None)
                elif change == 'namespace': self.catalogue['SqlServerBrowser'] = ('SqlServerBrowser', 'SqlServer', True, 'IdentityPlatform.Administrator.Tests.Features.Accounts', 'Accounts')
                elif change == 'story': del self.matrix['administrator_stories']['Accounts']
                with self.assertRaises(ValueError): self.verify()


if __name__ == '__main__':
    unittest.main()
