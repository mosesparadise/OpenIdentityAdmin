"""Independent handcrafted build-provenance and exact-input coverage controls."""
import copy
import hashlib
import contextlib
import io
import json
import os
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch

import check_coverage_bounds as gate
import coverage_identity as identity

MODULE = 'IdentityPlatform.Core'
POLICY = {'requiredModules': [MODULE], 'minimumLineCoverage': 0.9, 'minimumBranchCoverage': 0.8}
XML = b'''<coverage><packages><package name="IdentityPlatform.Core"><classes><class name="Example.Feature" filename="/_/src/Feature.cs"><methods><method name="Execute" signature="System.Void()"><lines><line number="10" hits="1" branch="true" condition-coverage="100% (2/2)"><conditions><condition number="0" type="jump" coverage="100%"/></conditions></line></lines></method></methods><lines><line number="10" hits="1" branch="true" condition-coverage="100% (2/2)"><conditions><condition number="0" type="jump" coverage="100%"/></conditions></line></lines></class></classes></package></packages></coverage>'''


def sha(value):
    return hashlib.sha256(value).hexdigest()


def fixture_matrix():
    """Handwritten retained-browser obligations, independent of production selector."""
    cells = []
    for provider in ('PostgreSql', 'SqlServer'):
        for browser in ('chromium', 'webkit'):
            cells.append({'provider': provider, 'browser': browser, 'partition': 'standard', 'browser_story': '', 'label': provider + '-' + browser})
            for story in ('Accounts', 'Security', 'BrowserSessions', 'BrowserLogin', 'BrowserProtection', 'ApplicationEditor', 'ApplicationAuthority', 'ApplicationLifecycle', 'ManagedConsent'):
                cells.append({'provider': provider, 'browser': browser, 'partition': 'administrator', 'browser_story': story, 'label': 'administrator-' + provider + '-' + browser + '-' + story.lower()})
    return {'include': cells}


def fixture_labels(matrix):
    """The current one hundred required cells, built without gate helper imports."""
    labels = ['unit', 'contracts-core', 'contracts-chromium', 'contracts-webkit']
    for provider in ('PostgreSql', 'SqlServer'):
        labels += [provider, provider + '-lifecycle-restart']
        labels += [provider + '-lifecycle-' + group for group in ('distribution', 'keyrotation-activation', 'keyrotation-retirement')]
        labels += [provider + '-apphost-' + group for group in ('localdemo', 'foundation', 'bffsessions')]
        labels += ['issuer-' + provider + '-' + group for group in ('flows', 'boundaries')]
        labels += ['administrator-' + provider + '-' + group for group in ('applicationgrants', 'managedapplications-admission', 'managedapplications-issuance', 'applications', 'sessionrefresh', 'browsersessions-foundations', 'browsersessions-product', 'browsersessions-lifecycle', 'protocoladmission', 'recovery', 'security', 'accounts', 'ceremonies', 'bootstrap', 'factors', 'governance-policy', 'governance-commands', 'governance-failures')]
    return labels + [cell['label'] for cell in matrix['include']]


class CoverageIdentityControls(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.inputs = self.root / 'inputs'
        self.merged = self.root / 'merged.xml'
        self.merged.write_bytes(XML)
        self.matrix = fixture_matrix()
        self.labels = fixture_labels(self.matrix)
        self.assertEqual(100, len(set(self.labels)))
        self.expected = {'commit': 'a' * 40, 'tree': 'b' * 40, 'runId': '12345', 'runAttempt': '1', 'configuration': 'Debug', 'framework': 'net10.0', 'format': 'cobertura', 'collectorSettingsSha256': 'c' * 64, 'toolManifestSha256': 'd' * 64}
        self.build = {**self.expected, 'sdk': '10.0.114', 'collector': '18.12.0+test', 'collectorPackageSha256': '9' * 64, 'modules': {MODULE: {'dllSha256': 'e' * 64, 'pdbSha256': 'f' * 64}}}
        for label in self.labels:
            report = self.report(label)
            report.parent.mkdir(parents=True)
            report.write_bytes(XML)
            self.manifest(label).write_text(json.dumps({'schema': 1, 'cell': label, 'build': self.build, 'reportSha256': sha(XML), 'observedModules': [MODULE], 'attestationLimit': 'Synthetic fixture, no runtime attestation.'}))
        self.first = self.labels[0]

    def report(self, label):
        return self.inputs / ('foundation-' + label) / 'coverage' / (label + '.cobertura.xml')

    def manifest(self, label):
        return self.report(label).with_name(label + '.identity.json')

    def change(self, mutate, label=None):
        path = self.manifest(label or self.first)
        value = json.loads(path.read_text())
        mutate(value)
        path.write_text(json.dumps(value))

    def evaluate(self):
        return gate.evaluate(self.inputs, self.matrix, self.merged, self.expected, POLICY)

    def test_complete_handcrafted_current_run_passes_exact_bounds_and_hashes(self):
        result = self.evaluate()
        self.assertEqual(100, len(result['inputs']))
        self.assertEqual(set(self.labels), {row['cell'] for row in result['inputs']})
        self.assertEqual({'module': MODULE, 'linesCovered': 1, 'linesTotal': 1, 'branchesCoveredLowerBound': 2, 'branchesTotal': 2, 'lineRate': 1, 'branchRateLowerBound': 1, 'zeroBranchPopulation': False}, result['modules'][0])
        for item in result['inputs']:
            self.assertEqual(sha(XML), item['reportSha256'])
            self.assertEqual(sha(self.manifest(item['cell']).read_bytes()), item['identitySha256'])
        self.assertEqual(sha(XML), result['mergedReportSha256'])

    def test_missing_input_rejects(self):
        self.report(self.first).unlink()
        with self.assertRaises(ValueError): self.evaluate()

    def test_extra_input_rejects_even_when_it_duplicates_valid_bytes(self):
        (self.inputs / 'extra.cobertura.xml').write_bytes(XML)
        with self.assertRaises(ValueError): self.evaluate()

    def test_relabelled_input_cannot_substitute_for_required_cell(self):
        self.report(self.first).rename(self.report(self.first).with_name('wrong.cobertura.xml'))
        with self.assertRaises(ValueError): self.evaluate()

    def test_missing_manifest_rejects(self):
        self.manifest(self.first).unlink()
        with self.assertRaises(ValueError): self.evaluate()

    def test_extra_manifest_rejects(self):
        (self.inputs / 'extra.identity.json').write_text(self.manifest(self.first).read_text())
        with self.assertRaises(ValueError): self.evaluate()

    def test_copied_other_cell_manifest_rejects(self):
        self.manifest(self.first).write_text(self.manifest(self.labels[1]).read_text())
        with self.assertRaises(ValueError): self.evaluate()

    def test_report_changed_after_manifest_rejects(self):
        self.report(self.first).write_bytes(XML.replace(b'Execute', b'Altered'))
        with self.assertRaises(ValueError): self.evaluate()

    def test_every_current_context_field_is_enforced(self):
        original = self.manifest(self.first).read_text()
        for field in self.expected:
            with self.subTest(field=field):
                self.manifest(self.first).write_text(original)
                self.change(lambda value: value['build'].__setitem__(field, 'stale'))
                with self.assertRaises(ValueError): self.evaluate()

    def test_all_cells_agree_on_sdk_collector_and_module_binaries(self):
        original = self.manifest(self.first).read_text()
        mutations = [lambda v: v['build'].__setitem__('sdk', '10.0.999'), lambda v: v['build'].__setitem__('collector', '0.0.0'), lambda v: v['build']['modules'][MODULE].__setitem__('dllSha256', '1' * 64), lambda v: v['build']['modules'][MODULE].__setitem__('pdbSha256', '2' * 64)]
        for index, mutate in enumerate(mutations):
            with self.subTest(mutation=index):
                self.manifest(self.first).write_text(original)
                self.change(mutate)
                with self.assertRaises(ValueError): self.evaluate()

    def test_missing_required_binary_manifest_entry_rejects(self):
        self.change(lambda value: value['build']['modules'].clear())
        with self.assertRaises(ValueError): self.evaluate()

    def test_extra_binary_manifest_entry_rejects(self):
        self.change(lambda value: value['build']['modules'].__setitem__('Unreviewed', {'dllSha256': '1' * 64, 'pdbSha256': '2' * 64}))
        with self.assertRaises(ValueError): self.evaluate()

    def test_missing_and_invalid_dll_pdb_hashes_reject(self):
        original = self.manifest(self.first).read_text()
        for field in ['dllSha256', 'pdbSha256']:
            for value in [None, '', 'f' * 63, 'G' * 64, 123]:
                with self.subTest(field=field, value=value):
                    self.manifest(self.first).write_text(original)
                    self.change(lambda row: row['build']['modules'][MODULE].__setitem__(field, value))
                    with self.assertRaises(ValueError): self.evaluate()
            self.manifest(self.first).write_text(original)
            self.change(lambda row: row['build']['modules'][MODULE].pop(field))
            with self.assertRaises(ValueError): self.evaluate()

    def test_observed_module_substitution_rejects(self):
        self.change(lambda value: value.__setitem__('observedModules', ['Substituted']))
        with self.assertRaises(ValueError): self.evaluate()

    def test_duplicate_json_key_rejects_including_nested_module_binding(self):
        path = self.manifest(self.first)
        original = path.read_text()
        for old, new in [('"schema": 1', '"schema": 1, "schema": 1'), ('"dllSha256":', '"dllSha256": "' + 'e' * 64 + '", "dllSha256":')]:
            path.write_text(original.replace(old, new, 1))
            with self.subTest(field=old), self.assertRaises(ValueError): self.evaluate()

    def test_merged_source_line_change_rejects_despite_unchanged_native_branch_summary(self):
        self.merged.write_bytes(XML.replace(b'number="10"', b'number="11"'))
        with self.assertRaises(ValueError): self.evaluate()

    def test_merged_branch_loss_does_not_hide_raw_population(self):
        self.merged.write_bytes(XML.replace(b' condition-coverage="100% (2/2)"', b'').replace(b'<conditions><condition number="0" type="jump" coverage="100%"/></conditions>', b''))
        self.assertEqual(2, self.evaluate()['modules'][0]['branchesTotal'])

    def test_identity_schema_is_integer_not_boolean(self):
        original = self.manifest(self.first).read_text()
        for schema in [True, 1.0, '1']:
            self.manifest(self.first).write_text(original)
            self.change(lambda value: value.__setitem__('schema', schema))
            with self.subTest(schema=repr(schema)), self.assertRaises(ValueError): self.evaluate()


    def test_exact_schema_rejects_extra_and_missing_fields(self):
        original = self.manifest(self.first).read_text()
        for mutate in [lambda v: v.__setitem__('unexpected', 1), lambda v: v.pop('attestationLimit'), lambda v: v['build'].__setitem__('unexpected', 1), lambda v: v['build'].pop('collectorPackageSha256')]:
            self.manifest(self.first).write_text(original)
            self.change(mutate)
            with self.assertRaises(ValueError): self.evaluate()

    def test_package_identity_and_sdk_cannot_be_missing_or_malformed(self):
        original = self.manifest(self.first).read_text()
        for field, value in [('collectorPackageSha256', 'x' * 64), ('collectorPackageSha256', ''), ('sdk', ''), ('sdk', '9.0.100'), ('collector', '')]:
            self.manifest(self.first).write_text(original)
            self.change(lambda row: row['build'].__setitem__(field, value))
            with self.subTest(field=field, value=value), self.assertRaises(ValueError): self.evaluate()

    def test_different_collector_package_between_cells_rejects(self):
        self.change(lambda row: row['build'].__setitem__('collectorPackageSha256', '8' * 64))
        with self.assertRaises(ValueError): self.evaluate()

    def cli(self, collector='18.12.0+test'):
        repository = self.root / 'repository'
        (repository / 'eng').mkdir(parents=True)
        (repository / 'eng/coverage-policy.json').write_text(json.dumps(POLICY))
        settings = b'<settings/>'
        tools = json.dumps({'tools': {'dotnet-coverage': {'version': '18.12.0'}}}).encode()
        (repository / 'eng/coverage.xml').write_bytes(settings)
        (repository / 'dotnet-tools.json').write_bytes(tools)
        for label in self.labels:
            self.change(lambda value: value['build'].update(collectorSettingsSha256=sha(settings), toolManifestSha256=sha(tools), collector=collector), label)
        output = self.root / 'result.json'
        command = lambda *args: 'b' * 40 if args[-1] == 'HEAD^{tree}' else 'a' * 40
        with patch.object(gate, 'ROOT', repository), patch.object(gate, 'command', side_effect=command), patch.dict(os.environ, {'GITHUB_RUN_ID': '12345', 'GITHUB_RUN_ATTEMPT': '1'}), patch.object(sys, 'argv', ['check_coverage_bounds.py', str(self.inputs), json.dumps(self.matrix), str(self.merged), str(output)]), contextlib.redirect_stdout(io.StringIO()):
            gate.main()
        return output

    def test_cli_publishes_only_current_complete_bound_evidence(self):
        output = self.cli()
        result = json.loads(output.read_text())
        self.assertEqual(100, len(result['inputs']))
        self.assertEqual('12345', result['build']['runId'])
        self.assertEqual(2, result['modules'][0]['branchesTotal'])

    def test_cli_rejects_consistent_but_unpinned_collector_without_receipt(self):
        with self.assertRaises(ValueError): self.cli('18.11.0')
        self.assertFalse((self.root / 'result.json').exists())


class BuildCopyControls(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        (self.root / 'eng').mkdir()
        (self.root / 'eng/coverage-policy.json').write_text(json.dumps(POLICY))
        (self.root / 'eng/coverage.xml').write_text('<settings/>')
        (self.root / 'dotnet-tools.json').write_text(json.dumps({'tools': {'dotnet-coverage': {'version': '18.12.0'}}}))
        self.cache = self.root / 'packages'
        package = self.cache / 'dotnet-coverage/18.12.0/dotnet-coverage.18.12.0.nupkg'
        package.parent.mkdir(parents=True)
        package.write_bytes(b'pinned collector package')
        self.paths = []
        for folder in ['src/Product', 'tests/Fixture']:
            dll = self.root / folder / 'bin/Debug/net10.0' / (MODULE + '.dll')
            dll.parent.mkdir(parents=True)
            dll.write_bytes(b'exact DLL bytes')
            dll.with_suffix('.pdb').write_bytes(b'exact PDB bytes')
            self.paths.append(dll)

    def command(self, *args):
        values = {('git', 'status', '--porcelain', '--untracked-files=no'): '', ('git', 'rev-parse', 'HEAD'): 'a' * 40, ('git', 'rev-parse', 'HEAD^{tree}'): 'b' * 40, ('dotnet', 'coverage', '--version'): '18.12.0+test', ('dotnet', '--version'): '10.0.114', ('dotnet', 'nuget', 'locals', 'global-packages', '--list'): 'global-packages: ' + str(self.cache)}
        return values[args]

    def context_patches(self):
        self.enterContext(patch.object(identity, 'ROOT', self.root))
        self.enterContext(patch.object(identity, 'command', side_effect=self.command))
        self.enterContext(patch.dict(os.environ, {'GITHUB_RUN_ID': '12345', 'GITHUB_RUN_ATTEMPT': '1'}))

    def invoke(self, action):
        with patch.object(sys, 'argv', ['coverage_identity.py', action, 'cell-test']):
            identity.main()

    def test_matching_build_copies_have_independent_expected_byte_hashes(self):
        self.assertEqual({MODULE: {'dllSha256': sha(b'exact DLL bytes'), 'pdbSha256': sha(b'exact PDB bytes')}}, identity.build_modules(self.root))

    def test_missing_module_or_symbols_reject(self):
        self.paths[0].with_suffix('.pdb').unlink()
        with self.assertRaises(ValueError): identity.build_modules(self.root)
        for path in self.paths: path.unlink()
        with self.assertRaises(ValueError): identity.build_modules(self.root)

    def test_one_changed_dll_copy_rejects(self):
        self.paths[1].write_bytes(b'different DLL')
        with self.assertRaises(ValueError): identity.build_modules(self.root)

    def test_one_changed_pdb_copy_rejects(self):
        self.paths[1].with_suffix('.pdb').write_bytes(b'different PDB')
        with self.assertRaises(ValueError): identity.build_modules(self.root)

    def test_clean_context_binds_current_run_settings_and_pinned_collector(self):
        self.context_patches()
        value = identity.context()
        self.assertEqual(('12345', '1', '18.12.0+test'), (value['runId'], value['runAttempt'], value['collector']))
        self.assertEqual(sha(b'<settings/>'), value['collectorSettingsSha256'])
        self.assertEqual(sha(b'pinned collector package'), value['collectorPackageSha256'])
        self.assertEqual(sha((self.root / 'dotnet-tools.json').read_bytes()), value['toolManifestSha256'])

    def test_dirty_tracked_source_rejects(self):
        self.context_patches()
        command = self.command
        with patch.object(identity, 'command', side_effect=lambda *args: ' M src/file.cs' if args[:2] == ('git', 'status') else command(*args)):
            with self.assertRaises(ValueError): identity.context()

    def test_unpinned_collector_rejects(self):
        self.context_patches()
        command = self.command
        with patch.object(identity, 'command', side_effect=lambda *args: '18.11.0' if args == ('dotnet', 'coverage', '--version') else command(*args)):
            with self.assertRaises(ValueError): identity.context()

    def test_before_after_emits_exact_report_and_observed_module_binding(self):
        self.context_patches()
        self.invoke('before')
        folder = self.root / 'artifacts/coverage'
        (folder / 'cell-test.cobertura.xml').write_bytes(XML)
        self.invoke('after')
        result = json.loads((folder / 'cell-test.identity.json').read_text())
        self.assertEqual(('cell-test', sha(XML), [MODULE]), (result['cell'], result['reportSha256'], result['observedModules']))
        self.assertIn('not runtime loaded-module attestation', result['attestationLimit'])

    def test_changed_build_between_before_after_rejects_even_when_all_copies_match(self):
        self.context_patches()
        self.invoke('before')
        for path in self.paths: path.write_bytes(b'new consistent DLL')
        with self.assertRaises(ValueError): self.invoke('after')

    def test_stale_before_snapshot_cannot_rebind_new_run_attempt(self):
        self.context_patches()
        self.invoke('before')
        with patch.dict(os.environ, {'GITHUB_RUN_ATTEMPT': '2'}):
            with self.assertRaises(ValueError): self.invoke('after')

    def test_changed_collector_package_between_before_after_rejects(self):
        self.context_patches()
        self.invoke('before')
        (self.cache / 'dotnet-coverage/18.12.0/dotnet-coverage.18.12.0.nupkg').write_bytes(b'changed package')
        with self.assertRaises(ValueError): self.invoke('after')

    def test_changed_settings_between_before_after_rejects(self):
        self.context_patches()
        self.invoke('before')
        (self.root / 'eng/coverage.xml').write_text('<changed/>')
        with self.assertRaises(ValueError): self.invoke('after')

    def test_before_refuses_existing_snapshot(self):
        self.context_patches()
        self.invoke('before')
        with self.assertRaises(ValueError): self.invoke('before')


if __name__ == '__main__':
    unittest.main(verbosity=2)
