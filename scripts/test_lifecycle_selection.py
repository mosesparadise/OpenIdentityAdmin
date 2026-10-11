import copy
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as E
from test_selection import catalogue, require_lifecycle_partition, require_lifecycle_execution
from test_evidence import require_complete_trx


class LifecycleSelectionTests(unittest.TestCase):
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
            self.assertEqual(matrix['profiles'][provider]['Issuer.Lifecycle'], 13)
        self.assertNotIn('Issuer.Lifecycle', matrix['profiles']['unit'])
        self.assertNotIn('Issuer.Lifecycle', matrix['browser'])

    def test_lifecycle_browser_namespace_cannot_hide_without_a_trait(self):
        test = {'uid': 'a', 'displayName': 'A', 'traits': [{'key': 'Provider', 'value': 'PostgreSql'}],
                'type': {'namespace': 'IdentityPlatform.Issuer.Lifecycle.Tests.Features.Restart'}}
        catalogue(json.dumps({'schemaVersion': 1, 'tests': [test]}), lifecycle=True)
        test['type']['namespace'] = 'IdentityPlatform.Issuer.Lifecycle.Tests.Features.Browser'
        with self.assertRaises(ValueError):
            catalogue(json.dumps({'schemaVersion': 1, 'tests': [test]}), lifecycle=True)

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


if __name__ == '__main__':
    unittest.main()
