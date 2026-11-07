"""Independent handcrafted controls for the raw Cobertura lower-bound gate."""
import copy
from pathlib import Path
import sys
import tempfile
import unittest
import xml.etree.ElementTree as ET

from coverage_bounds import Evidence, read_report, statistics, merged_line_map

MODULE = 'Example.Product'
POLICY = {'requiredModules': [MODULE], 'minimumLineCoverage': 0, 'minimumBranchCoverage': 0}


def line(number=10, hits='1', branch='true', declaration='50% (1/2)', conditions=((0, 'jump', '50%'),)):
    element = ET.Element('line', number=str(number), hits=str(hits), branch=branch)
    if declaration is not None:
        element.set('condition-coverage', declaration)
    if conditions:
        parent = ET.SubElement(element, 'conditions')
        for identity, kind, coverage in conditions:
            ET.SubElement(parent, 'condition', number=str(identity), type=kind, coverage=coverage)
    return element


def document(lines=None, module=MODULE, method='Execute', signature='System.Void()'):
    """Construct a small independent method/class-union fixture, not expected gate output."""
    lines = [line()] if lines is None else lines
    root = ET.Element('coverage')
    package = ET.SubElement(ET.SubElement(root, 'packages'), 'package', name=module)
    cls = ET.SubElement(ET.SubElement(package, 'classes'), 'class', name='Example.Feature', filename='/_/src/Feature.cs')
    meth = ET.SubElement(ET.SubElement(cls, 'methods'), 'method', name=method, signature=signature)
    body = ET.SubElement(meth, 'lines')
    union = ET.SubElement(cls, 'lines')
    for value in lines:
        body.append(copy.deepcopy(value))
        union.append(copy.deepcopy(value))
    return root


class CoverageBoundsControls(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.serial = 0

    def save(self, root):
        self.serial += 1
        path = Path(self.temp.name) / f'{self.serial}.xml'
        ET.ElementTree(root).write(path, encoding='utf-8')
        return path

    def read(self, root):
        return read_report(self.save(root))

    def aggregate(self, *roots):
        result = Evidence()
        for root in roots:
            result.add(self.read(root))
        return result

    def counts(self, evidence, **floors):
        rows = statistics(evidence, {**POLICY, **floors})
        self.assertEqual(1, len(rows))
        return rows[0]

    def test_complementary_distinct_jump_conditions_prove_four_outcomes(self):
        a = document([line(declaration='50% (2/4)', conditions=((0, 'jump', '100%'), (1, 'jump', '0%')))])
        b = document([line(declaration='50% (2/4)', conditions=((0, 'jump', '0%'), (1, 'jump', '100%')))])
        row = self.counts(self.aggregate(a, b))
        self.assertEqual((4, 4), (row['branchesCoveredLowerBound'], row['branchesTotal']))

    def test_repeated_fifty_percent_does_not_infer_opposite_outcomes(self):
        row = self.counts(self.aggregate(document(), document()))
        self.assertEqual((1, 2), (row['branchesCoveredLowerBound'], row['branchesTotal']))

    def test_permutation_and_duplicate_observation_are_idempotent(self):
        a = document()
        b = document([line(declaration='100% (2/2)', conditions=((0, 'jump', '100%'),))])
        self.assertEqual(self.counts(self.aggregate(a, b)), self.counts(self.aggregate(b, a, b)))

    def test_switch_uses_maximum_whole_line_without_invented_branch_ids(self):
        a = document([line(declaration='50% (2/4)', conditions=((0, 'switch', '50%'),))])
        b = document([line(declaration='75% (3/4)', conditions=((0, 'switch', '75%'),))])
        row = self.counts(self.aggregate(a, b))
        self.assertEqual((3, 4), (row['branchesCoveredLowerBound'], row['branchesTotal']))

    def test_mixed_conditions_do_not_sum_cross_report_jump_improvement(self):
        a = document([line(declaration='50% (3/6)', conditions=((0, 'jump', '100%'), (1, 'switch', '25%')))])
        b = document([line(declaration='50% (3/6)', conditions=((0, 'jump', '0%'), (1, 'switch', '75%')))])
        row = self.counts(self.aggregate(a, b))
        self.assertEqual((3, 6), (row['branchesCoveredLowerBound'], row['branchesTotal']))

    def test_one_retained_declaration_supplies_missing_observation_denominator(self):
        partial = document([line(declaration=None, conditions=())])
        row = self.counts(self.aggregate(partial, document(), partial))
        self.assertEqual((1, 2), (row['branchesCoveredLowerBound'], row['branchesTotal']))

    def test_branch_without_any_declaration_rejects(self):
        with self.assertRaises(ValueError):
            self.counts(self.aggregate(document([line(declaration=None, conditions=())])))

    def test_lost_merged_conditions_cannot_reduce_raw_denominator(self):
        original = document([line(declaration='100% (8/8)', conditions=tuple((i, 'jump', '100%') for i in range(4)))])
        raw = self.aggregate(original)
        merged = copy.deepcopy(original)
        child = merged.find('.//method/lines/line')
        child.attrib.pop('condition-coverage')
        child.remove(child.find('conditions'))
        self.assertEqual((raw.modules, raw.lines), merged_line_map(self.save(merged)))
        row = self.counts(raw)
        self.assertEqual((8, 8), (row['branchesCoveredLowerBound'], row['branchesTotal']))

    def test_method_identity_keeps_shared_source_line_populations_distinct(self):
        root = document()
        parent = root.find('.//methods')
        second = copy.deepcopy(parent[0])
        second.set('name', 'OtherMethod')
        parent.append(second)
        summary = root.find('.//class/lines/line')
        summary.set('condition-coverage', '50% (2/4)')
        ET.SubElement(summary.find('conditions'), 'condition', number='1', type='jump', coverage='50%')
        row = self.counts(self.aggregate(root))
        self.assertEqual((1, 1, 2, 4), (row['linesCovered'], row['linesTotal'], row['branchesCoveredLowerBound'], row['branchesTotal']))

    def test_method_signature_disambiguates_overloads(self):
        root = document()
        parent = root.find('.//methods')
        second = copy.deepcopy(parent[0])
        second.set('signature', 'System.Void(System.Int32)')
        parent.append(second)
        summary = root.find('.//class/lines/line')
        summary.set('condition-coverage', '50% (2/4)')
        ET.SubElement(summary.find('conditions'), 'condition', number='1', type='jump', coverage='50%')
        self.assertEqual(4, self.counts(self.aggregate(root))['branchesTotal'])

    def test_required_zero_branch_module_is_explicit(self):
        row = self.counts(self.aggregate(document([line(branch='false', declaration=None, conditions=())])), minimumBranchCoverage=1)
        self.assertTrue(row['zeroBranchPopulation'])
        self.assertEqual((0, 0, 1), (row['branchesCoveredLowerBound'], row['branchesTotal'], row['branchRateLowerBound']))

    def test_exact_branch_floor_passes_one_outcome_below_fails(self):
        passing = document([line(declaration='80% (8/10)', conditions=tuple((i, 'jump', '100%' if i < 4 else '0%') for i in range(5)))])
        failing = document([line(declaration='70% (7/10)', conditions=tuple((i, 'jump', '100%' if i < 3 else '50%' if i == 3 else '0%') for i in range(5)))])
        self.assertEqual(8, self.counts(self.aggregate(passing), minimumBranchCoverage=0.8)['branchesCoveredLowerBound'])
        with self.assertRaises(ValueError):
            self.counts(self.aggregate(failing), minimumBranchCoverage=0.8)

    def test_exact_line_floor_passes_below_fails(self):
        root = document([line(number=i+1, hits='1' if i < 9 else '0', branch='false', declaration=None, conditions=()) for i in range(10)])
        self.assertEqual(9, self.counts(self.aggregate(root), minimumLineCoverage=0.9)['linesCovered'])
        root.find('.//method/lines/line').set('hits', '0')
        root.find('.//class/lines/line').set('hits', '0')
        with self.assertRaises(ValueError):
            self.counts(self.aggregate(root), minimumLineCoverage=0.9)

    def test_missing_and_duplicate_required_module_reject(self):
        raw = self.aggregate(document())
        for required in [['Absent'], [MODULE, MODULE]]:
            with self.subTest(required=required), self.assertRaises(ValueError):
                statistics(raw, {**POLICY, 'requiredModules': required})

    def test_each_required_module_enforces_its_own_floor(self):
        for failing in [MODULE, 'Second.Product']:
            roots = [document(module=module, lines=[line()] if module == failing else [line(declaration='100% (2/2)', conditions=((0, 'jump', '100%'),))]) for module in [MODULE, 'Second.Product']]
            raw = self.aggregate(*roots)
            with self.subTest(failing=failing), self.assertRaises(ValueError):
                statistics(raw, {**POLICY, 'requiredModules': [MODULE, 'Second.Product'], 'minimumBranchCoverage': 0.8})

    def test_class_method_union_mismatch_rejects(self):
        root = document()
        root.find('.//class/lines/line').set('hits', '0')
        with self.assertRaises(ValueError):
            self.read(root)

    def test_duplicate_module_class_method_and_line_reject(self):
        for parent_path in ['./packages', './/classes', './/methods', './/method/lines', './/class/lines']:
            root = document()
            parent = root.find(parent_path)
            parent.append(copy.deepcopy(parent[0]))
            with self.subTest(parent=parent_path), self.assertRaises(ValueError):
                self.read(root)

    def test_blank_identities_reject(self):
        for path, attribute in [('.//package', 'name'), ('.//class', 'name'), ('.//class', 'filename'), ('.//method', 'name'), ('.//method', 'signature')]:
            root = document()
            root.find(path).set(attribute, ' ')
            with self.subTest(path=path, attribute=attribute), self.assertRaises(ValueError):
                self.read(root)

    def test_invalid_hits_and_line_numbers_reject(self):
        for attribute, values in [('hits', ['-1', '1.5', 'NaN', 'Infinity', '']), ('number', ['0', '-1', '1.5', 'NaN'])]:
            for value in values:
                root = document()
                root.find('.//method/lines/line').set(attribute, value)
                with self.subTest(attribute=attribute, value=value), self.assertRaises(ValueError):
                    self.read(root)

    def test_malformed_condition_metadata_rejects(self):
        invalid = [
            line(branch='unknown'), line(branch='false'),
            line(declaration='100% (3/2)'), line(declaration='NaN% (1/2)'),
            line(declaration='100% (1/2)'), line(declaration='50% (-1/2)'),
            line(hits='0'), line(conditions=((0, 'jump', '25%'),)),
            line(conditions=((0, 'unknown', '50%'),)),
            line(conditions=((0, 'jump', 'NaN%'),)),
            line(conditions=((0, 'jump', '101%'),)),
            line(conditions=((0, 'jump', '50%'), (0, 'jump', '50%'))),
            line(conditions=((0, 'jump', '100%'),)), line(conditions=()),
            line(declaration=None),
        ]
        for index, value in enumerate(invalid):
            with self.subTest(case=index), self.assertRaises(ValueError):
                self.read(document([value]))

    def test_inconsistent_cross_report_shapes_reject(self):
        variants = [line(declaration='50% (2/4)', conditions=((0, 'jump', '50%'), (1, 'jump', '50%'))),
                    line(conditions=((1, 'jump', '50%'),)), line(conditions=((0, 'switch', '50%'),)),
                    line(branch='false', declaration=None, conditions=())]
        for value in variants:
            with self.subTest(value=ET.tostring(value)), self.assertRaises(ValueError):
                self.aggregate(document(), document([value]))

    def test_declared_branch_with_zero_population_rejects(self):
        with self.assertRaises(ValueError):
            self.counts(self.aggregate(document([line(declaration='0% (0/0)', conditions=())])))

    def test_single_switch_percentage_must_match_its_known_line_population(self):
        for declaration, coverage in [('0% (0/4)', '100%'), ('100% (4/4)', '0%')]:
            with self.subTest(declaration=declaration), self.assertRaises(ValueError):
                self.read(document([line(declaration=declaration, conditions=((0, 'switch', coverage),))]))

    def test_one_switch_in_mixed_point_must_match_residual_population(self):
        with self.assertRaises(ValueError):
            self.read(document([line(declaration='100% (6/6)', conditions=((0, 'jump', '100%'), (1, 'switch', '0%')))]))

    def test_raw_class_branch_loss_or_disagreement_rejects(self):
        for target, change in [('.//class/lines/line', 'false'), ('.//method/lines/line', 'false')]:
            root = document()
            node = root.find(target)
            node.set('branch', change)
            node.attrib.pop('condition-coverage')
            node.remove(node.find('conditions'))
            with self.subTest(target=target), self.assertRaises(ValueError):
                self.read(root)
        root = document()
        node = root.find('.//method/lines/line')
        node.attrib.pop('condition-coverage')
        node.remove(node.find('conditions'))
        with self.assertRaises(ValueError):
            self.read(root)

    def test_native_float_percentage_noise_preserves_exact_counts(self):
        root = document([line(declaration='66.66666666666666% (4/6)', conditions=((0, 'switch', '66.66666666666666%'),))])
        self.assertEqual(4, self.counts(self.aggregate(root))['branchesCoveredLowerBound'])
        with self.assertRaises(ValueError):
            self.read(document([line(declaration='66.66% (4/6)', conditions=((0, 'switch', '66.66%'),))]))

    def test_multiple_switch_populations_reject_as_ambiguous(self):
        with self.assertRaises(ValueError):
            self.read(document([line(declaration='50% (2/4)', conditions=((0, 'switch', '50%'), (1, 'switch', '50%')))]))

    def test_empty_reports_reject(self):
        with self.assertRaises(ValueError):
            self.read(ET.Element('coverage'))

    def test_merged_identity_loss_or_changed_hits_is_observable(self):
        raw = self.aggregate(document())
        for attribute, value in [('filename', '/_/src/Different.cs'), ('name', 'Different.Class')]:
            root = document()
            root.find('.//class').set(attribute, value)
            self.assertNotEqual((raw.modules, raw.lines), merged_line_map(self.save(root)))
        root = document()
        root.find('.//class/lines/line').set('hits', '0')
        self.assertNotEqual((raw.modules, raw.lines), merged_line_map(self.save(root)))


if __name__ == '__main__':
    unittest.main(verbosity=2)
