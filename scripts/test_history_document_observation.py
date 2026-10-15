"""Seed finite evidence faults without claiming native error-document accessibility."""
import copy
import json
from pathlib import Path
import tempfile
import unittest
import xml.etree.ElementTree as ET
from history_document_receipt import combine_receipts, require_document
from test_history_navigation_observation import synthetic


def evidence():
    """Return explicitly synthetic positive input, never a native result."""
    return {'category': 'Read', 'prior_verified': True, 'commits_at_catch': 0,
            'commits_before_read': 1, 'commits_after_read': 1, 'responses': 0, 'same_frame': True, 'posts': 1,
            'document': dict(CurrentRoot=True, InternalError=True, ErrorCode=True, Target=True, Location=True,
                             Ready=True, Secret=False, Recovery=False)}


class DocumentControls(unittest.TestCase):
    def check(self, value, extra=None):
        """Attach one projected line to a synthetic native-shape fixture and run the real validator."""
        root = synthetic('Passed')
        output = root.find('./Results/UnitTestResult/Output/StdOut')
        output.text += '\nhistory-document ' + json.dumps(value)
        if extra is not None:
            output.text += '\n' + extra
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'synthetic.trx'
            ET.ElementTree(root).write(path)
            return require_document(path)

    def test_Given_ExactFiniteIdentity_When_Checked_Then_NoOwnershipOrAcceptanceIsGranted(self):
        result = self.check(evidence())
        self.assertTrue(result['document_identity_supported'])
        for key in ('navigation_ownership_proved', 'acceptance_credit', 'causal_closure'):
            self.assertFalse(result[key])

    def test_Given_StaleSecretOrWrongIdentity_When_Checked_Then_IdentityIsNotSupported(self):
        for key in evidence()['document']:
            value = evidence()
            value['document'][key] = not value['document'][key]
            with self.subTest(key=key): self.assertFalse(self.check(value)['document_identity_supported'])
        for key, replacement in [('prior_verified', False), ('same_frame', False), ('responses', 1),
                                 ('posts', 2), ('commits_before_read', 0), ('commits_after_read', 2)]:
            with self.subTest(key=key):
                self.assertFalse(self.check(evidence() | {key: replacement})['document_identity_supported'])

    def test_Given_UnavailableDocument_When_Checked_Then_GapRemainsExplicit(self):
        for category in ('NoCacheMiss', 'Timeout', 'Cancelled', 'Unavailable', 'Unexpected', 'CleanupIncomplete'):
            value = evidence() | {'category': category, 'document': None}
            result = self.check(value)
            self.assertFalse(result['document_identity_supported'])
            self.assertEqual(category, result['document_observation']['category'])

    def test_Given_IncompleteDocument_When_Combined_Then_OverallReceiptCannotClaimCompleteness(self):
        for category in ('Read', 'Timeout', 'Unavailable'):
            value = evidence() | {'category': category}
            document = self.check(value)
            combined = combine_receipts({'evidence_complete': True, 'native_outcome': 'Passed'}, document)
            self.assertTrue(combined['navigation_evidence_complete'])
            self.assertEqual(category == 'Read', combined['evidence_complete'])
            self.assertEqual('Passed', combined['native_outcome'])

    def test_Given_MalformedDuplicateOrSaturatedEvidence_When_Checked_Then_ValidationFails(self):
        inputs = [evidence() | {'extra': 'private'}, evidence() | {'posts': True},
                  evidence() | {'responses': 1000}, evidence() | {'commits_at_catch': 2},
                  evidence() | {'category': 'unknown'}, evidence() | {'document': None}]
        for change in ({'Secret': 'false'}, {'extra': 'private'}):
            value = copy.deepcopy(evidence()); value['document'].update(change); inputs.append(value)
        for value in inputs:
            with self.subTest(value=value), self.assertRaises(ValueError): self.check(value)
        with self.assertRaises(ValueError): self.check(evidence(), 'history-document {}')
        from history_document_receipt import unique_object
        with self.assertRaises(ValueError): unique_object([('Secret', False), ('Secret', True)])


if __name__ == '__main__':
    unittest.main()
