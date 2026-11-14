"""Seed substitutions against the separate sample unit mutation identities."""
import copy
import json
import unittest
from sample_mutation_selection import NAMESPACE, sample_catalogue, sample_filter, sample_identity
from control_runner import run


class SampleMutationSelectionTests(unittest.TestCase):
    """Require closed native discovery before any sample mutation can execute."""

    def test_valid_fact_and_exact_identity(self):
        row = self.row()
        result = sample_catalogue(json.dumps({'schemaVersion': 1, 'tests': [row]}))
        self.assertTrue(sample_identity(result, {'method': row['displayName'], 'id': 'sample-state-cas'}))
        self.assertFalse(sample_identity(result, {'method': row['displayName'] + 'Other', 'id': 'sample-state-cas'}))
        self.assertFalse(sample_identity(result, {'method': row['displayName'], 'id': 'sample-ticket-removal'}))
        self.assertEqual('CriticalDecision=sample-state-cas', sample_filter('sample-state-cas'))
        with self.assertRaises(ValueError): sample_filter('unreviewed')

    def test_substitutions_and_missing_or_duplicate_rows_reject(self):
        for defect in ('namespace', 'provider', 'browser', 'decision', 'ambiguous', 'display', 'duplicate', 'empty'):
            row = self.row()
            rows = [row]
            if defect == 'namespace': row['type']['namespace'] = 'Other'
            elif defect == 'provider': row['traits'].append({'key': 'Provider', 'value': 'SqlServer'})
            elif defect == 'browser': row['traits'].append({'key': 'Category', 'value': 'Browser'})
            elif defect == 'decision': row['traits'][0]['value'] = 'unknown'
            elif defect == 'ambiguous': row['traits'].append(copy.deepcopy(row['traits'][0]))
            elif defect == 'display': row['displayName'] = 'Other.Case'
            elif defect == 'duplicate': rows.append(copy.deepcopy(row))
            else: rows = []
            with self.subTest(defect=defect), self.assertRaises(ValueError):
                sample_catalogue(json.dumps({'schemaVersion': 1, 'tests': rows}))

    @staticmethod
    def row():
        return {'uid': 'fixed', 'displayName': NAMESPACE + '.SampleStateTests.Given_State_When_Redeemed_Then_OnlyOneWins',
                'type': {'namespace': NAMESPACE, 'typeName': 'SampleStateTests'},
                'traits': [{'key': 'CriticalDecision', 'value': 'sample-state-cas'}]}


if __name__ == '__main__':
    run(2)
