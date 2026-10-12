#!/usr/bin/env python3
import unittest
from test_selection import require_administrator_partition, select_projects


class AdministratorSelectionTests(unittest.TestCase):
    def setUp(self):
        self.catalogue = {p: (p + '-case', p, False, 'IdentityPlatform.Administrator.Tests.Features.Bootstrap') for p in ('PostgreSql', 'SqlServer')}
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
                elif mutation == 'unknown-namespace': self.catalogue['SqlServer'] = ('SqlServer-case', 'SqlServer', False, 'Unexpected')
                elif mutation == 'wrong-provider': self.catalogue['SqlServer'] = ('SqlServer-case', 'Other', False, 'IdentityPlatform.Administrator.Tests.Features.Bootstrap')
                elif mutation == 'browser': self.catalogue['SqlServer'] = ('SqlServer-case', 'SqlServer', True, 'IdentityPlatform.Administrator.Tests.Features.Bootstrap')
                with self.assertRaises(ValueError): self.verify()

    def test_isolation_and_default_all(self):
        matrix = {'profiles': {'PostgreSql': {'Issuer': 2, 'Issuer.Lifecycle': 3, 'Administrator': 1}, 'unit': {'Core': 1}}, 'browser': {'Issuer': 4}}
        self.assertEqual({'Issuer': 2}, select_projects(matrix, 'PostgreSql', partition='standard'))
        self.assertEqual({'Administrator': 1}, select_projects(matrix, 'PostgreSql', partition='administrator'))
        self.assertEqual(matrix['profiles']['PostgreSql'], select_projects(matrix, 'PostgreSql'))
        for profile, browser in [('unit', None), ('PostgreSql', 'webkit')]:
            with self.assertRaises(ValueError): select_projects(matrix, profile, browser, 'administrator')


if __name__ == '__main__':
    unittest.main()
