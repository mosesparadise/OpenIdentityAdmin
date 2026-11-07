"""Exercise the temporary closed source-location projection without copying report data."""
import json
import unittest
import xml.etree.ElementTree as ET
from verify_mutations import pilot_witness_frames, PILOT_WITNESS_METHODS


class PilotWitnessReceiptTests(unittest.TestCase):
    def test_known_source_line_retains_only_closed_method_and_bounded_number(self):
        root = ET.Element('Result')
        stack = ET.SubElement(root, 'StackTrace')
        method = PILOT_WITNESS_METHODS[1]
        stack.text = ('   at ' + method + '(synthetic-secret) in /synthetic-secret/file.cs:line 42\n'
                      '   at Unreviewed.SecretMethod(synthetic-secret) in /private/path.cs:line 17\n'
                      '   at ' + method + '(synthetic-secret) in /private/file.cs:line 100000\n'
                      '   at Prefix' + method + '(synthetic-secret) in /private/file.cs:line 12')
        result = pilot_witness_frames(root)
        self.assertEqual([{'method': method, 'line': 42}], result)
        self.assertNotIn('synthetic-secret', json.dumps(result))
        self.assertNotIn('/private', json.dumps(result))


if __name__ == '__main__':
    unittest.main()
