import copy
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from check_supply_chain import require_no_vulnerabilities, inventory_licenses


class AdvisoryEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.path = str(Path("project.csproj").resolve())
        self.report = {"version": 1, "sources": ["https://api.nuget.org/v3/index.json"], "projects": [{"path": self.path}]}

    def test_complete_clean_inventory_passes(self):
        self.assertEqual(1, require_no_vulnerabilities(self.report, {self.path}))

    def test_missing_duplicate_and_lookup_errors_fail(self):
        for transform in (lambda r: r.update(projects=[]), lambda r: r["projects"].append(r["projects"][0]),
                          lambda r: r.update(problems=[{"text": "source unavailable"}]),
                          lambda r: r.update(sources=[])):
            with self.subTest(transform=transform):
                report = copy.deepcopy(self.report)
                transform(report)
                with self.assertRaises(ValueError):
                    require_no_vulnerabilities(report, {self.path})

    def test_top_level_and_transitive_vulnerabilities_fail(self):
        for group in ("topLevelPackages", "transitivePackages"):
            report = copy.deepcopy(self.report)
            report["projects"][0]["frameworks"] = [{group: [{"id": "synthetic", "resolvedVersion": "1.0.0", "vulnerabilities": [{"severity": "High"}]}]}]
            with self.assertRaisesRegex(ValueError, "Known vulnerability"):
                require_no_vulnerabilities(report, {self.path})

    def test_missing_license_inventory_fails(self):
        with tempfile.TemporaryDirectory() as temporary:
            with self.assertRaisesRegex(ValueError, "No locked dependencies"):
                inventory_licenses(Path(temporary), Path(temporary), {})


class LicenseEvidenceTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.cache = self.root / "cache"
        self.package = self.cache / "sample" / "1.0.0"
        self.package.mkdir(parents=True)
        self.lock = {"dependencies": {"net10.0": {"Sample": {"type": "Direct", "resolved": "1.0.0", "contentHash": "synthetic-hash"}}}}
        (self.root / "packages.lock.json").write_text(json.dumps(self.lock))
        self.policy = {"allowedExpressions": ["MIT"], "reviewedExceptions": {}}

    def declaration(self, kind, text):
        (self.package / "sample.nuspec").write_text(f'<package><metadata><license type="{kind}">{text}</license></metadata></package>')

    def test_allowlisted_expression_is_recorded(self):
        self.declaration("expression", "MIT")
        self.assertEqual("MIT", inventory_licenses(self.root, self.cache, self.policy)[0]["licenseDeclaration"])

    def test_unreviewed_declaration_is_rejected(self):
        self.declaration("expression", "GPL-3.0-only")
        with self.assertRaisesRegex(ValueError, "Unreviewed license"):
            inventory_licenses(self.root, self.cache, self.policy)

    def test_reviewed_text_is_accepted_but_changed_text_is_rejected(self):
        self.declaration("file", "LICENSE")
        text = self.package / "LICENSE"
        text.write_text("Synthetic reviewed terms")
        self.policy["reviewedExceptions"]["Sample/1.0.0"] = {"file": "LICENSE", "sha256": hashlib.sha256(text.read_bytes()).hexdigest(), "disposition": "fixture"}
        self.assertEqual("fixture", inventory_licenses(self.root, self.cache, self.policy)[0]["disposition"])
        text.write_text("Changed terms")
        with self.assertRaisesRegex(ValueError, "evidence changed"):
            inventory_licenses(self.root, self.cache, self.policy)

    def test_inconsistent_lock_hash_is_rejected(self):
        other = self.root / "other"
        other.mkdir()
        self.lock["dependencies"]["net10.0"]["Sample"]["contentHash"] = "different"
        (other / "packages.lock.json").write_text(json.dumps(self.lock))
        with self.assertRaisesRegex(ValueError, "Inconsistent locked"):
            inventory_licenses(self.root, self.cache, self.policy)


if __name__ == "__main__":
    unittest.main()
