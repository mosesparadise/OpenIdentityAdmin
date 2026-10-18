import copy
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as E
from test_selection import catalogue, require_lifecycle_partition, require_lifecycle_execution, select_projects, lifecycle_invocations, require_lifecycle_story
from test_evidence import require_complete_trx


class LifecycleSelectionTests(unittest.TestCase):
    def test_Given_ScheduledPartitions_When_Combined_Then_EveryReviewedProviderCaseAppearsExactlyOnce(self):
        """Keep the independently reviewed provider total and disjoint project partition obligations explicit."""
        matrix = json.loads((Path(__file__).resolve().parents[1] / 'eng/test-matrix.json').read_text())
        for provider in ('PostgreSql', 'SqlServer'):
            complete = select_projects(matrix, provider)
            standard = select_projects(matrix, provider, partition='standard')
            lifecycle = select_projects(matrix, provider, partition='lifecycle')
            self.assertFalse(standard.keys() & lifecycle.keys())
            administrator = select_projects(matrix, provider, partition='administrator')
            self.assertFalse(administrator.keys() & (standard.keys() | lifecycle.keys()))
            self.assertEqual(complete, standard | lifecycle | administrator)
            self.assertEqual(316, sum(standard.values()))
            self.assertEqual({'Issuer.Lifecycle': 41}, lifecycle)
            expected_administrator = {'PostgreSql': 499, 'SqlServer': 604}
            expected_total = {'PostgreSql': 856, 'SqlServer': 961}
            self.assertEqual({'Administrator': expected_administrator[provider]}, administrator)
            self.assertEqual(expected_total[provider], sum(complete.values()))
        self.assertEqual(matrix['profiles']['unit'], select_projects(matrix, 'unit'))
        self.assertEqual(matrix['browser'], select_projects(matrix, 'SqlServer', 'webkit'))
        for profile, browser, partition in [('unit', None, 'standard'), ('unit', None, 'lifecycle'),
                                             ('SqlServer', 'webkit', 'lifecycle'),
                                             ('SqlServer', None, 'unknown')]:
            with self.assertRaises(ValueError):
                select_projects(matrix, profile, browser, partition)

    def test_exact_provider_catalogues_cannot_be_omitted_substituted_or_contaminated(self):
        all_tests = {'a': ('A', 'PostgreSql', False), 'b': ('B', 'SqlServer', False)}
        selected = {'a': all_tests['a']}
        counts = {'PostgreSql': 1, 'SqlServer': 1}
        require_lifecycle_partition(all_tests, selected, 'PostgreSql', counts)
        cases = [({}, selected, counts), (selected, selected, counts),
                 (all_tests, {}, counts), (all_tests, {'b': all_tests['b']}, counts),
                 (all_tests, {'a': ('Substitute', 'PostgreSql', False)}, counts),
                 (dict(all_tests, c=('Browser', 'PostgreSql', True)), selected, counts),
                 (dict(all_tests, c=all_tests['a']), selected, counts),
                 (all_tests, selected, {'PostgreSql': 1})]
        for catalogue, subset, expected in cases:
            with self.subTest(catalogue=catalogue, subset=subset):
                with self.assertRaises(ValueError):
                    require_lifecycle_partition(catalogue, subset, 'PostgreSql', expected)

    def test_manifest_requires_both_lifecycle_provider_invocations(self):
        matrix = json.loads((Path(__file__).resolve().parents[1] / 'eng/test-matrix.json').read_text())
        self.assertEqual(matrix['projects']['Issuer.Lifecycle'], {'group': 'aspire'})
        for provider in ('PostgreSql', 'SqlServer'):
            self.assertEqual(matrix['profiles'][provider]['Issuer.Lifecycle'], 41)
        self.assertNotIn('Issuer.Lifecycle', matrix['profiles']['unit'])
        self.assertNotIn('Issuer.Lifecycle', matrix['browser'])

    def test_lifecycle_browser_namespace_cannot_hide_without_a_trait(self):
        test = {'uid': 'a', 'displayName': 'A', 'traits': [{'key': 'Provider', 'value': 'PostgreSql'}],
                'type': {'namespace': 'IdentityPlatform.Issuer.Lifecycle.Tests.Features.Restart'}}
        catalogue(json.dumps({'schemaVersion': 1, 'tests': [test]}), lifecycle=True)
        test['type']['namespace'] = 'IdentityPlatform.Issuer.Lifecycle.Tests.Features.Browser'
        with self.assertRaises(ValueError):
            catalogue(json.dumps({'schemaVersion': 1, 'tests': [test]}), lifecycle=True)

    def test_story_partitions_are_exact_disjoint_exhaustive_and_fail_closed(self):
        matrix = json.loads((Path(__file__).resolve().parents[1] / 'eng/test-matrix.json').read_text())
        tests = {}
        for story, counts in matrix['lifecycle_stories'].items():
            for provider, count in counts.items():
                for i in range(count):
                    uid = f'{story}-{provider}-{i}'
                    tests[uid] = (uid, provider, False, 'IdentityPlatform.Issuer.Lifecycle.Tests.Features.' + story)
        for provider in ('PostgreSql', 'SqlServer'):
            self.assertEqual([('Restart', 13), ('Distribution', 23), ('KeyRotation.Activation', 3), ('KeyRotation.Retirement', 2)], lifecycle_invocations(matrix, provider))
            subsets = []
            for story in matrix['lifecycle_stories']:
                selected = {uid: t for uid, t in tests.items() if t[1] == provider and t[3].endswith('.' + story)}
                subsets.append(selected)
                require_lifecycle_story(tests, selected, provider, story, matrix)
                for substitute in ({}, dict(selected, unrelated=next(iter(tests.values())))):
                    with self.assertRaises(ValueError):
                        require_lifecycle_story(tests, substitute, provider, story, matrix)
                key = next(iter(selected))
                for corrupt in [dict(tests, **{key: (*tests[key][:3], 'Unreviewed.Namespace')}),
                                dict(tests, **{key: (tests[key][0], 'Unknown', False, tests[key][3])}),
                                dict(tests, **{key: (tests[key][0], provider, True, tests[key][3])}),
                                {k: v for k, v in tests.items() if k != key}]:
                    with self.assertRaises(ValueError):
                        require_lifecycle_story(corrupt, selected, provider, story, matrix)
            union = {}
            for selected in subsets:
                self.assertFalse(union.keys() & selected.keys())
                union.update(selected)
            self.assertEqual({uid: t for uid, t in tests.items() if t[1] == provider}, union)
        for mutate in ('omitted', 'total', 'unknown', 'provider'):
            corrupt = copy.deepcopy(matrix)
            if mutate == 'omitted': del corrupt['lifecycle_stories']['Restart']
            elif mutate == 'total': corrupt['profiles']['PostgreSql']['Issuer.Lifecycle'] += 1
            elif mutate == 'unknown': corrupt['lifecycle_stories']['Unreviewed'] = {'PostgreSql': 1, 'SqlServer': 1}
            else: del corrupt['lifecycle_stories']['Distribution']['SqlServer']
            with self.assertRaises(ValueError): lifecycle_invocations(corrupt, 'PostgreSql')
        with self.assertRaises(ValueError): lifecycle_invocations(matrix, 'PostgreSql', 'Unknown')

    def test_contradictory_results_cannot_pass_with_successful_counters(self):
        original = E.fromstring('''<TestRun><Results><UnitTestResult testId="t" executionId="e" testName="A" outcome="Passed"/></Results>
<TestDefinitions><UnitTest id="t" name="A"><Execution id="e"/></UnitTest></TestDefinitions>
<TestEntries><TestEntry testId="t" executionId="e"/></TestEntries>
<ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1"/></ResultSummary></TestRun>''')
        zeros = ('failed', 'error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted',
                 'notRunnable', 'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending')
        for key in zeros:
            original.find('./ResultSummary/Counters').set(key, '0')
        def validate(root):
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'result.trx'
                E.ElementTree(root).write(path)
                require_complete_trx(path, expected_count=1)
                require_lifecycle_execution(path, {'a': ('A', 'PostgreSql', False)})
        validate(original)
        mutations = []
        for key in zeros:
            root = copy.deepcopy(original); root.find('./ResultSummary/Counters').set(key, '1'); mutations.append(root)
            root = copy.deepcopy(original); del root.find('./ResultSummary/Counters').attrib[key]; mutations.append(root)
        root = copy.deepcopy(original); root.find('ResultSummary').append(copy.deepcopy(root.find('./ResultSummary/Counters'))); mutations.append(root)

        for attribute, value in [('outcome', 'Failed'), ('outcome', 'NotExecuted'), ('testName', 'Substitute'), ('testId', ''), ('executionId', '')]:
            root = copy.deepcopy(original); root.find('./Results/UnitTestResult').set(attribute, value); mutations.append(root)
        for parent in ('Results', 'TestDefinitions', 'TestEntries'):
            root = copy.deepcopy(original); root.find(parent).clear(); mutations.append(root)
            root = copy.deepcopy(original); root.find(parent).append(copy.deepcopy(root.find(parent)[0])); mutations.append(root)
        for outcome in ('Failed', 'Aborted', 'InProgress'):
            root = copy.deepcopy(original); root.find('ResultSummary').set('outcome', outcome); mutations.append(root)
        root = copy.deepcopy(original); E.SubElement(root.find('ResultSummary'), 'RunInfo', outcome='Error'); mutations.append(root)
        root = copy.deepcopy(original); E.SubElement(root.find('ResultSummary'), 'ErrorInfo'); mutations.append(root)
        root = copy.deepcopy(original); root.find('./TestDefinitions/UnitTest/Execution').set('id', 'wrong'); mutations.append(root)
        root = copy.deepcopy(original); root.find('./TestEntries/TestEntry').set('executionId', 'wrong'); mutations.append(root)
        for root in mutations:
            with self.assertRaises(ValueError):
                validate(root)


class ExecutionBindingTests(unittest.TestCase):
    def report(self):
        """Create one internally consistent successful native-report shape without product data."""
        root = E.fromstring('''<TestRun><Results><UnitTestResult testId="t" executionId="e" testName="A" outcome="Passed"/></Results>
<TestDefinitions><UnitTest id="t" name="A"><Execution id="e"/></UnitTest></TestDefinitions>
<TestEntries><TestEntry testId="t" executionId="e"/></TestEntries>
<ResultSummary outcome="Completed"><Counters total="1" executed="1" passed="1"/></ResultSummary></TestRun>''')
        for key in ('failed', 'error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted',
                    'notRunnable', 'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending'):
            root.find('./ResultSummary/Counters').set(key, '0')
        return root

    def test_Given_SuccessfulReport_When_DefinitionRepeatsExecution_Then_AmbiguousBindingIsRejected(self):
        """Neither identical nor conflicting extra execution bindings can hide behind a valid first child."""
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'report.trx'
            original = self.report()
            E.ElementTree(original).write(path)
            selected = {'native-id': ('A', 'PostgreSql', False)}
            require_lifecycle_execution(path, selected)
            for execution_id in ('e', 'different'):
                invalid = copy.deepcopy(original)
                E.SubElement(invalid.find('./TestDefinitions/UnitTest'), 'Execution', id=execution_id)
                E.ElementTree(invalid).write(path)
                with self.subTest(execution_id=execution_id), self.assertRaisesRegex(ValueError, 'execution'):
                    require_lifecycle_execution(path, selected)


if __name__ == '__main__':
    unittest.main()
