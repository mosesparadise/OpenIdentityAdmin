"""Exercise the real coverage CLI with bounded synthetic reports."""
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from xml.etree.ElementTree import Element, SubElement, ElementTree

ROOT = Path(__file__).resolve().parents[1]
POLICY = json.loads((ROOT / "eng/coverage-policy.json").read_text())


class CoverageGateTests(unittest.TestCase):
    def run_gate(self, missing=False, line=None, branch=None):
        root = Element("coverage")
        packages = SubElement(root, "packages")
        for index, name in enumerate(POLICY["requiredModules"]):
            if missing and index == 0:
                continue
            SubElement(packages, "package", {"name": name, "line-rate": str(line if index == 0 and line is not None else .9),
                                            "branch-rate": str(branch if index == 0 and branch is not None else .8)})
        with tempfile.TemporaryDirectory() as temp:
            report = Path(temp) / "coverage.xml"
            ElementTree(root).write(report)
            return subprocess.run([sys.executable, str(ROOT / "scripts/check-coverage.py"), str(report)], capture_output=True, text=True, timeout=10)

    def test_every_module_at_exact_floors_passes(self):
        self.assertEqual(0, self.run_gate().returncode)

    def test_one_missing_module_fails(self):
        result = self.run_gate(missing=True)
        self.assertEqual(1, result.returncode)
        self.assertIn("no execution evidence", result.stdout)

    def test_each_floor_is_enforced_independently(self):
        self.assertEqual(1, self.run_gate(line=.8999, branch=1).returncode)
        self.assertEqual(1, self.run_gate(line=1, branch=.7999).returncode)


if __name__ == "__main__":
    unittest.main()
