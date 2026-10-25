#!/usr/bin/env python3
import unittest
import json
import tempfile
from pathlib import Path
from administrator_inventory import load_inventory, metadata, require_native_inventory
from test_selection import catalogue
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
        self.matrix = {'administrator_stories': {s: {p: 1 for p in ('PostgreSql', 'SqlServer')} for s in ('Bootstrap', 'Factors', 'Ceremonies', 'Accounts', 'Recovery', 'Security', 'Governance.Policy', 'Governance.Commands', 'Governance.Failures', 'ProtocolAdmission', 'BrowserSessions')},
                       'profiles': {p: {'Administrator': 11} for p in ('PostgreSql', 'SqlServer')}}
        self.catalogue = {p + s: (p + s, p, False, 'IdentityPlatform.Administrator.Tests.Features.' + s, None)
                          for p in ('PostgreSql', 'SqlServer') for s in ('Bootstrap', 'Factors', 'Ceremonies', 'Accounts', 'Recovery', 'Security', 'Governance.Policy', 'Governance.Commands', 'Governance.Failures', 'ProtocolAdmission', 'BrowserSessions')}
        self.inventory = {p: [p + s for s in ('Bootstrap', 'Factors', 'Ceremonies', 'Accounts', 'Recovery', 'Security', 'Governance.Policy', 'Governance.Commands', 'Governance.Failures', 'ProtocolAdmission', 'BrowserSessions')] for p in ('PostgreSql', 'SqlServer')}
        self.selected = {'PostgreSqlFactors': self.catalogue['PostgreSqlFactors']}

    def verify(self):
        require_administrator_story(self.catalogue, self.selected, 'PostgreSql', 'Factors', self.matrix, self.inventory)

    def test_control_and_invocation_isolation(self):
        self.verify()
        self.assertEqual([('Bootstrap', 1), ('Factors', 1), ('Ceremonies', 1), ('Accounts', 1), ('Recovery', 1), ('Security', 1), ('Governance.Policy', 1), ('Governance.Commands', 1), ('Governance.Failures', 1), ('ProtocolAdmission', 1), ('BrowserSessions', 1)], administrator_invocations(self.matrix, 'PostgreSql'))
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
                elif change == 'aggregate': self.matrix['profiles']['SqlServer']['Administrator'] = 8
                elif change == 'balanced-drift':
                    stories['Factors']['SqlServer'] = 2
                    stories['Bootstrap']['SqlServer'] = 2
                    self.matrix['profiles']['SqlServer']['Administrator'] = 13
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


class ProtocolAdmissionSelectionTests(AdministratorStoryTests):
    """Keep the new admission partition mandatory and isolated without substituting aggregate counts."""
    def test_Given_BrowserSessionStory_When_SelectingEitherProvider_Then_ExactCasesAndHostedLanesAreRequired(self):
        workflow = (Path(__file__).resolve().parents[1] / '.github/workflows/verify.yml').read_text()
        for provider in ('PostgreSql', 'SqlServer'):
            self.setUp()
            name = provider + 'BrowserSessions'
            require_administrator_story(self.catalogue, {name: self.catalogue[name]}, provider,
                                        'BrowserSessions', self.matrix, self.inventory)
            for invalid in ({}, {provider + 'ProtocolAdmission': self.catalogue[provider + 'ProtocolAdmission']}):
                with self.assertRaises(ValueError):
                    require_administrator_story(self.catalogue, invalid, provider, 'BrowserSessions', self.matrix, self.inventory)
            for group in ('Foundations', 'Product', 'Lifecycle'):
                cell = '{ profile: ' + provider + ', partition: administrator, story: BrowserSessions, browser_session_group: ' + group + ', label: administrator-' + provider + '-browsersessions-' + group.lower() + ' }'
                self.assertEqual(1, workflow.count(cell))
            del self.matrix['administrator_stories']['BrowserSessions']
            self.matrix['administrator_stories']['Factors'][provider] += 1
            with self.assertRaises(ValueError):
                administrator_invocations(self.matrix, provider)

    def test_Given_AdmissionStory_When_SelectingEitherProvider_Then_OnlyExactRunIdentitiesPass(self):
        for provider in ('PostgreSql', 'SqlServer'):
            self.setUp()
            name = provider + 'ProtocolAdmission'
            selected = {name: self.catalogue[name]}
            require_administrator_story(self.catalogue, selected, provider, 'ProtocolAdmission', self.matrix, self.inventory)
            self.assertEqual([('ProtocolAdmission', 1)], administrator_invocations(self.matrix, provider, 'ProtocolAdmission'))
            for invalid in ({}, {'stale-uid': self.catalogue[name]},
                            {provider + 'Factors': self.catalogue[provider + 'Factors']},
                            {'SqlServerProtocolAdmission' if provider == 'PostgreSql' else 'PostgreSqlProtocolAdmission':
                             self.catalogue['SqlServerProtocolAdmission' if provider == 'PostgreSql' else 'PostgreSqlProtocolAdmission']}):
                with self.assertRaises(ValueError):
                    require_administrator_story(self.catalogue, invalid, provider, 'ProtocolAdmission', self.matrix, self.inventory)

    def test_Given_BalancedTotals_When_AdmissionStoryIsRemoved_Then_SelectionRejectsIt(self):
        del self.matrix['administrator_stories']['ProtocolAdmission']
        for provider in ('PostgreSql', 'SqlServer'):
            self.matrix['administrator_stories']['Factors'][provider] += 1
            with self.assertRaises(ValueError): administrator_invocations(self.matrix, provider)

    def test_Given_AdmissionCli_When_ProfilePartitionOrBrowserConflicts_Then_NoExecutionStarts(self):
        import subprocess
        import sys
        script = Path(__file__).with_name('verify.py')
        for arguments in (['unit', '--partition', 'administrator'], ['PostgreSql', '--partition', 'lifecycle'],
                          ['SqlServer', '--partition', 'standard'], ['PostgreSql', '--partition', 'administrator', '--browser', 'chromium']):
            result = subprocess.run([sys.executable, str(script), *arguments, '--story', 'ProtocolAdmission'],
                                    capture_output=True, text=True, timeout=10)
            self.assertEqual(2, result.returncode)
            self.assertIn('error:', result.stderr)

    def test_Given_HostedAdmission_When_Configured_Then_BothProvidersRetainExistingDeadlines(self):
        root = Path(__file__).resolve().parents[1]
        workflow = (root / '.github/workflows/verify.yml').read_text()
        import re
        verify = re.split(r'\n  [A-Za-z][\w-]*:', workflow.split('  verify:\n', 1)[1], maxsplit=1)[0]
        self.assertIn('    timeout-minutes: 20', verify)
        for provider in ('PostgreSql', 'SqlServer'):
            cell = '{ profile: ' + provider + ', partition: administrator, story: ProtocolAdmission, label: administrator-' + provider + '-protocoladmission }'
            self.assertEqual(1, verify.count(cell))
        self.assertIn('timeout=180 if args.profile == "unit" else 660', (root / 'scripts/verify.py').read_text())


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


class AdministratorMetadataTests(unittest.TestCase):
    """Exercise portable full metadata independently of the within-run native UID binding."""
    def setUp(self):
        self.records = []
        for provider in ('PostgreSql', 'SqlServer'):
            self.records.append({'uid': provider, 'displayName': provider + '-case', 'type': {
                'assemblyFullName': 'IdentityPlatform.Administrator.Tests, Version=1.0.0.0',
                'namespace': 'IdentityPlatform.Administrator.Tests.Features.Governance.Policy',
                'typeName': 'PolicyTests', 'methodName': 'Given_State_When_Checked_Then_Denied',
                'methodArity': 0, 'returnTypeFullName': 'System.Threading.Tasks.Task',
                'parameterTypeFullNames': ['System.String']}, 'traits': [
                    {'key': 'Provider', 'value': provider}, {'key': 'CriticalDecision', 'value': 'control'}]})
        self.inventory = {'schemaVersion': 2, **{provider: [provider + '-case'] for provider in ('PostgreSql', 'SqlServer')},
                          'metadata': {row['displayName']: metadata(row) for row in self.records}}

    def verify(self, *, complete=True):
        """Validate seeded discovery without executing a provider or native test process."""
        require_native_inventory(json.dumps({'schemaVersion': 1, 'tests': self.records}), self.inventory, complete=complete)

    def test_Given_ReviewedNativeMetadata_When_TraitOrderAndLocationChange_Then_PortableInventoryMatches(self):
        for row in self.records:
            row['traits'].reverse()
            row['location'] = {'file': '/different/checkout/Test.cs', 'lineStart': 99}
        self.verify()

    def test_Given_ReviewedMetadata_When_AFieldChanges_Then_EqualCountDiscoveryIsRejected(self):
        for field, replacement in (('assemblyFullName', 'OtherAssembly'), ('namespace', 'OtherStory'),
                                   ('typeName', 'OtherType'), ('methodName', 'OtherMethod'), ('methodArity', 1), ('methodArity', False),
                                   ('returnTypeFullName', 'System.Void'), ('parameterTypeFullNames', ['System.Int32'])):
            with self.subTest(field=field):
                self.setUp()
                self.records[0]['type'] = {**self.records[0]['type'], field: replacement}
                with self.assertRaises(ValueError): self.verify()

    def test_Given_ReviewedCases_When_MissingDuplicateSubstitutedOrMisclassified_Then_DiscoveryIsRejected(self):
        for change in ('missing', 'duplicate-name', 'duplicate-uid', 'substitute', 'provider', 'trait', 'missing-trait', 'extra-trait', 'story'):
            with self.subTest(change=change):
                self.setUp()
                if change == 'missing': self.records.pop()
                elif change == 'duplicate-name': self.records[1]['displayName'] = self.records[0]['displayName']
                elif change == 'duplicate-uid': self.records[1]['uid'] = self.records[0]['uid']
                elif change == 'substitute': self.records[0]['displayName'] = 'unreviewed-case'
                elif change == 'provider': self.records[0]['traits'] = [{'key': 'Provider', 'value': 'SqlServer'}]
                elif change == 'trait': self.records[0]['traits'] = [{'key': 'Provider', 'value': 'PostgreSql'}, {'key': 'CriticalDecision', 'value': 'other'}]
                elif change == 'missing-trait': self.records[0]['traits'] = self.records[0]['traits'][:1]
                elif change == 'extra-trait': self.records[0]['traits'] = [*self.records[0]['traits'], {'key': 'Extra', 'value': 'unreviewed'}]
                else: self.records[0]['traits'] = [*self.records[0]['traits'], {'key': 'BrowserStory', 'value': 'Security'}]
                with self.assertRaises(ValueError): self.verify()

    def test_Given_FilteredDiscovery_When_MetadataChanges_Then_SubsetValidationAlsoRejectsIt(self):
        self.records = self.records[:1]
        self.verify(complete=False)
        with self.assertRaises(ValueError): self.verify()
        self.records[0]['type'] = {**self.records[0]['type'], 'methodName': 'OtherMethod'}
        with self.assertRaises(ValueError): self.verify(complete=False)

    def test_Given_NewOutputLocation_When_RunUidsChange_Then_MetadataPassesButSelectionStillRequiresExactRunUids(self):
        self.records = [{**row, 'uid': 'new-path-' + row['uid']} for row in self.records]
        self.verify()
        raw = json.dumps({'schemaVersion': 1, 'tests': self.records})
        all_tests = catalogue(raw, project='Administrator')
        selected = {self.records[0]['uid']: all_tests[self.records[0]['uid']]}
        counts = {'PostgreSql': 1, 'SqlServer': 1}
        names = {provider: self.inventory[provider] for provider in counts}
        require_administrator_partition(all_tests, selected, 'PostgreSql', counts, names)
        changed_uid = {'another-uid': next(iter(selected.values()))}
        with self.assertRaises(ValueError): require_administrator_partition(all_tests, changed_uid, 'PostgreSql', counts, names)

    def test_Given_VersionTwoManifest_When_Loaded_Then_ExactNamesAndMetadataAreRequired(self):
        for change in ('none', 'version', 'bool-version', 'duplicate-name', 'missing-record', 'extra-record', 'wrong-provider', 'malformed-trait'):
            with self.subTest(change=change), tempfile.TemporaryDirectory() as directory:
                self.setUp()
                if change == 'version': self.inventory['schemaVersion'] = 1
                elif change == 'bool-version': self.inventory['schemaVersion'] = True
                elif change == 'duplicate-name': self.inventory['PostgreSql'].append(self.inventory['PostgreSql'][0])
                elif change == 'missing-record': self.inventory['metadata'].pop('PostgreSql-case')
                elif change == 'extra-record': self.inventory['metadata']['other'] = self.inventory['metadata']['PostgreSql-case']
                elif change == 'wrong-provider': self.inventory['metadata']['PostgreSql-case']['traits'] = [{'key': 'Provider', 'value': 'SqlServer'}]
                elif change == 'malformed-trait': self.inventory['metadata']['PostgreSql-case']['traits'] = [{'key': 'Provider'}]
                path = Path(directory) / 'inventory.json'
                path.write_text(json.dumps(self.inventory))
                if change == 'none': self.assertEqual(self.inventory, load_inventory(path))
                else:
                    with self.assertRaises(ValueError): load_inventory(path)

    def test_Given_DuplicateJsonFields_When_LoadingManifestOrDiscovery_Then_LastValueDoesNotWin(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'inventory.json'
            path.write_text('{"schemaVersion":1,"schemaVersion":2}')
            with self.assertRaises(ValueError): load_inventory(path)
        with self.assertRaises(ValueError):
            require_native_inventory('{"schemaVersion":1,"schemaVersion":1,"tests":[]}', self.inventory)


if __name__ == '__main__':
    unittest.main()
