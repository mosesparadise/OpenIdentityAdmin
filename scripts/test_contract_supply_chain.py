import hashlib
import copy
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import check_contract_supply_chain as guard


class ContractSupplyChainTests(unittest.TestCase):
    def test_generator_gate_binds_adapted_bytes_versions_and_archive(self):
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            parser = 'node_modules/openapi-typescript/node_modules/@redocly/openapi-core'
            target = parser + '/lib/utils.js'
            before, after = 'const minimatch = require("minimatch");', 'const { minimatch } = require("minimatch");'
            original, adapted = (before + '\n'), (after + '\n')
            review = {'schemaVersion': 1, 'target': target, 'parserVersion': '1.34.23',
                      'generatorVersion': '7.13.0', 'matcherVersion': '11.0.4',
                      'upstreamIntegrity': 'sha512-synthetic', 'before': before, 'after': after,
                      'originalSha256': hashlib.sha256(original.encode()).hexdigest(),
                      'patchedSha256': hashlib.sha256(adapted.encode()).hexdigest()}
            versions = {parser: '1.34.23', 'node_modules/openapi-typescript': '7.13.0', 'node_modules/minimatch': '11.0.4'}
            lock = {'packages': {location: {'version': version, 'integrity': 'sha512-synthetic'} for location, version in versions.items()}}
            def write(source=adapted, candidate=None, policy=None, installed=None):
                for location, version in versions.items():
                    folder = root / location
                    folder.mkdir(parents=True, exist_ok=True)
                    (folder / 'package.json').write_text(json.dumps({'version': (installed or {}).get(location, version)}))
                (root / target).parent.mkdir(parents=True, exist_ok=True)
                (root / target).write_text(source)
                (root / 'package-lock.json').write_text(json.dumps(candidate or lock))
                (root / 'generator-compatibility.json').write_text(json.dumps(policy or review))
            write()
            guard.require_generator_compatibility(root, root)
            for source in [original, adapted + '// unrelated change', adapted * 2]:
                write(source=source)
                with self.assertRaises(ValueError): guard.require_generator_compatibility(root, root)
            for location in versions:
                changed = copy.deepcopy(lock)
                changed['packages'][location]['version'] = '0.0.0'
                write(candidate=changed)
                with self.assertRaises(ValueError): guard.require_generator_compatibility(root, root)
                write(installed={location: '0.0.0'})
                with self.assertRaises(ValueError): guard.require_generator_compatibility(root, root)
            changed = copy.deepcopy(lock)
            changed['packages'][parser]['integrity'] = 'sha512-substituted'
            write(candidate=changed)
            with self.assertRaises(ValueError): guard.require_generator_compatibility(root, root)
            write(policy=dict(review, originalSha256='0' * 64))
            with self.assertRaises(ValueError): guard.require_generator_compatibility(root, root)

    def test_normal_gate_rejects_missing_adaptation_before_advisory_acceptance(self):
        with patch.object(guard, 'install_cancellation_handler'), patch.object(guard, 'inventory_toolchain', return_value=[]), \
                patch.object(guard, 'require_generator_compatibility', side_effect=ValueError('missing adaptation')) as adaptation, \
                patch.object(guard, 'verify_renderers') as renderers, patch.object(guard, 'check_viewer_advisories') as viewers, \
                patch.object(guard, 'execute') as execute:
            with self.assertRaisesRegex(ValueError, 'missing adaptation'): guard.main()
            adaptation.assert_called_once_with()
            renderers.assert_not_called()
            viewers.assert_not_called()
            execute.assert_not_called()

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
