#!/usr/bin/env python3
"""Reject unreviewed driver changes and prove disposable native-visibility preparation."""
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest
from unittest.mock import patch
import run_native_visibility as target


class NativeVisibilityTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        cls.original = (target.DRIVER / target.BUNDLE).read_bytes()

    def test_each_engine_only_disables_its_own_override(self):
        for browser, (override, native, digest) in target.PATCHES.items():
            with self.subTest(browser=browser):
                result = target.native_bundle(self.original, target.VERSION, browser)
                self.assertEqual(hashlib.sha256(result).hexdigest(), digest)
                self.assertEqual(self.original, result.replace(native, override, 1))
                self.assertNotIn(override, result)
                for other, (marker, _, _) in target.PATCHES.items():
                    if other != browser:
                        self.assertEqual(result.count(marker), 1)

    def test_wrong_version_is_rejected(self):
        for browser in target.PATCHES:
            with self.subTest(browser=browser), self.assertRaises(ValueError):
                target.native_bundle(self.original, 'unreviewed', browser)

    def test_unsupported_engine_is_rejected(self):
        with self.assertRaises(ValueError):
            target.native_bundle(self.original, target.VERSION, 'unsupported')

    def test_modified_or_already_instrumented_bundle_is_rejected(self):
        for browser, (override, native, _) in target.PATCHES.items():
            for content in (self.original + b' ', self.original.replace(override, native)):
                with self.subTest(browser=browser), self.assertRaises(ValueError):
                    target.native_bundle(content, target.VERSION, browser)

    def test_absent_or_duplicate_marker_is_rejected_even_if_input_hash_is_rebased(self):
        for browser, (override, _, _) in target.PATCHES.items():
            for content in (self.original.replace(override, b''), self.original + override):
                with patch.object(target, 'ORIGINAL_SHA', hashlib.sha256(content).hexdigest()):
                    with self.assertRaises(ValueError):
                        target.native_bundle(content, target.VERSION, browser)

    def test_wrong_output_digest_is_rejected(self):
        for browser, (override, native, _) in target.PATCHES.items():
            with patch.dict(target.PATCHES, {browser: (override, native, 'unreviewed')}), self.assertRaises(ValueError):
                target.native_bundle(self.original, target.VERSION, browser)

    def test_disposable_copy_preserves_source_and_contains_exact_native_bundle(self):
        for browser in target.PATCHES:
            with self.subTest(browser=browser), tempfile.TemporaryDirectory() as directory:
                source = Path(directory) / 'source'
                bundle = source / target.BUNDLE
                bundle.parent.mkdir(parents=True)
                bundle.write_bytes(self.original)
                (source / 'package/package.json').write_text(json.dumps({'version': target.VERSION}))
                destination = Path(directory) / 'copy'
                target.prepare_driver(source, destination, browser)
                self.assertEqual(bundle.read_bytes(), self.original)
                self.assertEqual((destination / '.playwright' / target.BUNDLE).read_bytes(), target.native_bundle(self.original, target.VERSION, browser))


class NativeVisibilityPipelineTests(unittest.TestCase):
    def test_engine_scope_and_exact_coverage_obligations(self):
        for browser in ('chromium', 'webkit'):
            with self.subTest(browser=browser), patch.dict(target.os.environ, {'PLAYWRIGHT_DRIVER_SEARCH_PATH': 'unreviewed'}), \
                    patch.object(target, 'prepare_driver') as prepare, patch.object(target.subprocess, 'Popen') as manager, \
                    patch.object(target.subprocess, 'run') as run:
                run.return_value = subprocess.CompletedProcess([], 0, stdout='window id # 0x123')
                target.run('PostgreSql', browser, True)
                command = run.call_args_list[-1]
                self.assertEqual(command.args[0], ['dotnet', 'coverage', 'collect', '-s', 'eng/coverage.xml', '-o',
                    f'artifacts/coverage/administrator-PostgreSql-{browser}-applicationlifecycle.cobertura.xml', '-f', 'cobertura',
                    'python3', 'scripts/verify.py', 'PostgreSql', '--partition', 'administrator', '--browser', browser,
                    '--browser-story', 'ApplicationLifecycle'])
                prepare.assert_called_once()
                self.assertEqual(prepare.call_args.args[2], browser)
                self.assertEqual(command.kwargs['env']['PLAYWRIGHT_DRIVER_SEARCH_PATH'], str(prepare.call_args.args[1]))
                manager.return_value.terminate.assert_called_once()
                manager.return_value.wait.assert_called_once_with(timeout=3)

    def test_child_failure_propagates_and_joins_window_manager(self):
        with patch.object(target, 'prepare_driver'), patch.object(target.subprocess, 'Popen') as manager, patch.object(target.subprocess, 'run') as run:
            run.side_effect = [subprocess.CompletedProcess([], 0, stdout='window id # 0x123'), subprocess.CalledProcessError(1, 'bounded-test')]
            with self.assertRaises(subprocess.CalledProcessError):
                target.run('SqlServer', 'chromium', True)
            manager.return_value.terminate.assert_called_once()
            manager.return_value.wait.assert_called_once_with(timeout=3)

    def test_private_display_is_required_and_child_failure_is_not_swallowed(self):
        with patch.object(target.subprocess, 'run', side_effect=subprocess.CalledProcessError(1, 'xvfb')) as run:
            with self.assertRaises(subprocess.CalledProcessError):
                target.run('SqlServer', 'webkit', False)
            self.assertEqual(run.call_args.args[0], ['xvfb-run', '--auto-servernum', 'python3', target.__file__, 'SqlServer', 'webkit', '--desktop'])

    def test_unavailable_window_manager_fails_before_test_execution(self):
        with patch.object(target, 'prepare_driver'), patch.object(target.subprocess, 'Popen') as manager, patch.object(target.subprocess, 'run') as run:
            manager.return_value.poll.return_value = 1
            run.return_value = subprocess.CompletedProcess([], 1, stdout='')
            with self.assertRaises(RuntimeError):
                target.run('SqlServer', 'chromium', True)
            self.assertEqual(run.call_count, 1)
            manager.return_value.terminate.assert_called_once()
            manager.return_value.wait.assert_called_once_with(timeout=3)


if __name__ == '__main__':
    unittest.main()
