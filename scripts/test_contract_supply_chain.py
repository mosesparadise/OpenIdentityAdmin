import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import check_contract_supply_chain as guard


class ContractSupplyChainTests(unittest.TestCase):
    def test_candidate_scan_rejects_unreviewed_changed_duplicate_and_malformed_findings(self):
        reviewed = {'example': [{'id': 1, 'severity': 'high', 'url': 'https://github.com/advisories/synthetic'}]}
        guard.require_reviewed_advisories(reviewed, reviewed)
        guard.require_reviewed_advisories({}, reviewed)
        for current in [[], {'example': []}, {'example': {}}, {'example': [None]},
                        {'example': [{'id': '1'}]}, {'example': [{'id': 2}]},
                        {'example': [dict(reviewed['example'][0], severity='critical')]},
                        {'example': reviewed['example'] * 2}, {'substitute': reviewed['example']}]:
            with self.assertRaises(ValueError): guard.require_reviewed_advisories(current, reviewed)

    def test_lock_installed_identity_license_integrity_and_registry_cannot_drift(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            package = root / 'node_modules/alias'
            package.mkdir(parents=True)
            item = {'name': '@example/actual', 'version': '1.0.0', 'license': 'MIT', 'integrity': 'sha512-synthetic', 'resolved': 'https://registry.npmjs.org/example.tgz'}
            def write(changed=None, installed=None, stale=False):
                candidate = dict(item, **(changed or {}))
                (root / 'package-lock.json').write_text(json.dumps({'packages': {'': {}, 'node_modules/alias': candidate}}))
                review = {'lockSha256': 'stale' if stale else hashlib.sha256((root / 'package-lock.json').read_bytes()).hexdigest(), 'allowedExpressions': ['MIT']}
                (root / 'npm-review.json').write_text(json.dumps(review))
                (package / 'package.json').write_text(json.dumps(installed or item))
            with patch.object(guard, 'PACKAGE', root), patch.object(guard, 'POLICY', root):
                write()
                self.assertEqual('@example/actual', guard.inventory_toolchain()[0]['name'])
                for change in [{'license': 'Unknown'}, {'integrity': ''}, {'resolved': 'https://other.invalid/a.tgz'}, {'version': '2.0.0'}]:
                    write(change)
                    with self.assertRaises(ValueError): guard.inventory_toolchain()
                write(stale=True)
                with self.assertRaises(ValueError): guard.inventory_toolchain()
                write(installed=dict(item, name='alias'))
                with self.assertRaises(ValueError): guard.inventory_toolchain()
                write(); (package / 'package.json').unlink()
                with self.assertRaises(ValueError): guard.inventory_toolchain()


if __name__ == '__main__':
    unittest.main()
