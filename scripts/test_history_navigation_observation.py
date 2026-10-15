"""Seed malformed receipts and repeated launches without running a browser or claiming native evidence."""
import copy
from pathlib import Path
import tempfile
import unittest
import re
from unittest.mock import patch
import xml.etree.ElementTree as ET
from history_navigation_receipt import NATIVE_EXIT_TWO, require_receipt
from observe_history_navigation import METHOD, PARENT, REF, remaining, require_launch

NAME = METHOD + '(provider: "PostgreSql")'


def synthetic(outcome):
    """Create explicitly synthetic verifier input, never a native execution receipt."""
    root = ET.Element('TestRun')
    row = ET.SubElement(ET.SubElement(root, 'Results'), 'UnitTestResult',
                        testName=NAME, testId='synthetic-test', executionId='synthetic-execution', outcome=outcome)
    output = ET.SubElement(row, 'Output')
    catch = 0 if outcome == 'Passed' else 1
    records = ['phase=Setup response main=True navigation=True route=Form method=Get status=Ok',
               'phase=Setup commit main=True route=Form',
               'phase=Submit response main=True navigation=True route=Post method=Post status=Ok',
               'phase=Submit commit main=True route=Post',
               'phase=Safe response main=True navigation=True route=Safe method=Get status=Ok',
               'phase=Safe commit main=True route=Safe']
    if outcome == 'Failed': records.append('phase=Back commit main=True route=Post')
    records.append(f'phase=Back catch responses=0 commits={catch}')
    if outcome == 'Passed':
        records.append('phase=Complete phase')
    lines = [f'history-navigation ordinal={i} elapsed-ms={i} {value}' for i, value in enumerate(records, 1)]
    lines.append('history-navigation-final overflow=False posts=1')
    ET.SubElement(output, 'StdOut').text = '\n'.join(lines)
    if outcome == 'Failed':
        ET.SubElement(ET.SubElement(output, 'ErrorInfo'), 'Message').text = 'Microsoft.Playwright.PlaywrightException : NS_ERROR_DOCUMENT_NOT_CACHED\nCall log:'
    definition = ET.SubElement(ET.SubElement(root, 'TestDefinitions'), 'UnitTest', id='synthetic-test', name=NAME)
    ET.SubElement(definition, 'Execution', id='synthetic-execution')
    class_name, method = METHOD.rsplit('.', 1)
    ET.SubElement(definition, 'TestMethod', className=class_name, name=method)
    properties = ET.SubElement(definition, 'Properties')
    for key, value in [('Provider', 'PostgreSql'), ('Category', 'Browser'), ('BrowserStory', 'Accounts')]:
        prop = ET.SubElement(properties, 'Property')
        ET.SubElement(prop, 'Key').text = key
        ET.SubElement(prop, 'Value').text = value
    ET.SubElement(ET.SubElement(root, 'TestEntries'), 'TestEntry', testId='synthetic-test', executionId='synthetic-execution')
    summary = ET.SubElement(root, 'ResultSummary', outcome='Completed' if outcome == 'Passed' else 'Failed')
    counts = dict.fromkeys(('error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted', 'notRunnable',
                           'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending'), '0')
    counts.update(total='1', executed='1', passed=str(int(outcome == 'Passed')), failed=str(int(outcome == 'Failed')))
    ET.SubElement(summary, 'Counters', **counts)
    if outcome == 'Failed':
        info = ET.SubElement(ET.SubElement(summary, 'RunInfos'), 'RunInfo', outcome='Error')
        ET.SubElement(info, 'Text').text = NATIVE_EXIT_TWO
    return root


class ObservationControls(unittest.TestCase):
    def check(self, root, code):
        """Validate a temporary synthetic fixture; never write beside retained native evidence."""
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'synthetic.trx'
            ET.ElementTree(root).write(path)
            return require_receipt(path, NAME, code)

    def test_Given_CompleteSyntheticPassAndFailure_When_Checked_Then_OriginalOutcomeIsPreserved(self):
        for outcome, code in [('Passed', 0), ('Failed', 2)]:
            receipt = self.check(synthetic(outcome), code)
            self.assertEqual(outcome, receipt['native_outcome'])
            self.assertFalse(receipt['acceptance_credit'])
            self.assertFalse(receipt['causal_closure'])

    def test_Given_ChangedBindingsOrCounters_When_Checked_Then_ReceiptIsRejected(self):
        changes = [lambda r: r.find('.//UnitTestResult').set('testName', 'substituted'),
                   lambda r: r.find('.//TestEntry').set('executionId', 'substituted'),
                   lambda r: r.find('.//TestMethod').set('name', 'substituted'),
                   lambda r: r.find('.//Counters').set('notExecuted', '1'),
                   lambda r: r.find('.//Property/Value').__setattr__('text', 'SqlServer'),
                   lambda r: r.find('.//ResultSummary').set('outcome', 'Failed')]
        for change in changes:
            root = synthetic('Passed'); change(root)
            with self.subTest(change=changes.index(change)), self.assertRaises(ValueError):
                self.check(root, 0)

    def test_Given_AbsentOverflowedOrContradictoryLedger_When_Checked_Then_ReceiptIsIncomplete(self):
        changes = [lambda s: '', lambda s: s.replace('overflow=False', 'overflow=True'),
                   lambda s: s.replace('ordinal=2', 'ordinal=1'), lambda s: s.replace('posts=1', 'posts=2'),
                   lambda s: s.replace('commits=0', 'commits=1'), lambda s: s.replace('phase=Complete', 'phase=Back'),
                   lambda s: s.replace('elapsed-ms=1', 'elapsed-ms=999999'), lambda s: s + '\nhistory-navigation private=canary',
                   lambda s: s.replace('route=Safe', 'route=Other')]
        for change in changes:
            root = synthetic('Passed'); output = root.find('.//StdOut'); output.text = change(output.text)
            with self.subTest(change=changes.index(change)), self.assertRaises(ValueError):
                self.check(root, 0)

    def test_Given_UnexpectedNativeFailureOrExit_When_Checked_Then_NoCompleteTargetReceipt(self):
        root = synthetic('Failed'); root.find('.//Message').text = 'Other native failure'
        with self.assertRaises(ValueError): self.check(root, 2)
        with self.assertRaises(ValueError): self.check(synthetic('Passed'), 2)
        with self.assertRaises(ValueError): self.check(synthetic('Failed'), 0)

    def test_Given_ContradictoryOrAdditionalRunError_When_Checked_Then_NoCompleteReceipt(self):
        root = synthetic('Failed'); root.find('.//RunInfo/Text').text = 'Unrecognized native error'
        with self.assertRaises(ValueError): self.check(root, 2)
        root = synthetic('Failed'); root.find('.//RunInfos').append(copy.deepcopy(root.find('.//RunInfo')))
        with self.assertRaises(ValueError): self.check(root, 2)
        root = synthetic('Passed'); root.find('.//ResultSummary').append(copy.deepcopy(synthetic('Failed').find('.//RunInfos')))
        with self.assertRaises(ValueError): self.check(root, 0)

    def test_Given_EventAfterCatch_When_ClaimedByEarlierCounter_Then_ReceiptIsRejected(self):
        root = synthetic('Failed'); output = root.find('.//StdOut'); lines = output.text.splitlines()
        lines[6], lines[7] = lines[7], lines[6]
        output.text = '\n'.join(re.sub(r'ordinal=\d+ elapsed-ms=\d+', f'ordinal={i} elapsed-ms={i}', line)
                                for i, line in enumerate(lines, 1))
        with self.assertRaises(ValueError): self.check(root, 2)

    def test_Given_OnlyFirstNonForcedLaunch_When_Checked_Then_LaterTransitionsAreRejected(self):
        head = 'a' * 40
        env = {'GITHUB_RUN_ATTEMPT': '1', 'GITHUB_EVENT_NAME': 'push', 'GITHUB_REPOSITORY': 'mosesparadise/OpenIdentityAdmin',
               'GITHUB_REF': REF, 'GITHUB_SHA': head}
        event = {'ref': REF, 'before': PARENT, 'after': head, 'created': False, 'deleted': False, 'forced': False}
        require_launch(env, event, head, [PARENT])
        for key, value in [('before', head), ('created', True), ('forced', True), ('deleted', True), ('ref', 'other'), ('after', PARENT)]:
            changed = copy.deepcopy(event); changed[key] = value
            with self.subTest(key=key), self.assertRaises(RuntimeError): require_launch(env, changed, head, [PARENT])
        with self.assertRaises(RuntimeError): require_launch(env | {'GITHUB_RUN_ATTEMPT': '2'}, event, head, [PARENT])
        with self.assertRaises(RuntimeError): require_launch(env, event, head, [])

    def test_Given_ExpiredBudget_When_BeforeLaunch_Then_NoNewBudgetIsGranted(self):
        with patch('observe_history_navigation.time.monotonic', return_value=11), self.assertRaises(RuntimeError):
            remaining(10)


if __name__ == '__main__':
    unittest.main()
