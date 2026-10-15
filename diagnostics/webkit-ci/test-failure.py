"""Fixed original-failure classification and privacy controls; no runtime execution."""
import json
import unittest
from xml.sax.saxutils import escape
from failure import project

MESSAGE = 'Native certificate evidence wait expired; navigation=cancelled; exact failures=1; unrelated failures=0; page-closed=False; browser-connected=True; dropped=0; events=[ordinal=1 elapsed-ms=13 channel=request exact=True category=cancelled; ordinal=2 elapsed-ms=17 channel=navigation exact=True category=cancelled].'


def raw(message):
    return ('<TestRun><Results><UnitTestResult><Output><ErrorInfo><Message>' + escape(message)
            + '</Message><StackTrace>CertificateObservation.RequireAsync BrowserCertificateEvidence.RequireAsync private-secret-path</StackTrace>'
            + '</ErrorInfo></Output></UnitTestResult></Results></TestRun>').encode()


class FailureTests(unittest.TestCase):
    def test_original_format_retained_without_raw_stack(self):
        result = project(raw(MESSAGE))
        self.assertEqual(result['category'], 'certificate-evidence-expired')
        self.assertEqual(result['navigation'], 'cancelled'); self.assertEqual(len(result['events']), 2)
        self.assertTrue(result['certificate_wait_frame'])
        self.assertNotIn('private-secret', json.dumps(result))

    def test_unknown_failure_never_borrows_driver_classification(self):
        result = project(raw('private-secret-fixture failure'))
        self.assertEqual(result['category'], 'unknown'); self.assertNotIn('private-secret', json.dumps(result))

    def test_unrecognized_categories_and_suffix_stay_private(self):
        for value in (MESSAGE.replace('navigation=cancelled', 'navigation=private-secret'),
                      MESSAGE.replace('category=cancelled', 'category=private-secret'), MESSAGE + 'private-secret'):
            result = project(raw(value)); self.assertEqual(result['category'], 'unknown')
            self.assertNotIn('private-secret', json.dumps(result))

    def test_malformed_counts_and_event_order_stay_unknown(self):
        for value in (MESSAGE.replace('ordinal=2', 'ordinal=1'), MESSAGE.replace('exact failures=1', 'exact failures=3'),
                      MESSAGE.replace('elapsed-ms=13', 'elapsed-ms=9999999999999999999')):
            self.assertEqual(project(raw(value))['category'], 'unknown')

    def test_oversized_input_rejected(self):
        with self.assertRaises(AssertionError): project(b' ' * (2 * 1024 * 1024 + 1))

    def test_pass_has_no_failure(self):
        self.assertEqual(project(b'<TestRun/>')['category'], 'none')


if __name__ == '__main__': unittest.main()
