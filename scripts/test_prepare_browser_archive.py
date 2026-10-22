"""Prove mirror preparation preserves repository trust and fails before partial configuration."""
from pathlib import Path
import re
import subprocess
import tempfile
import unittest
from unittest.mock import patch
from control_runner import run
from prepare_browser_archive import archive_uri, main, prepare, rewrite

ROOT = Path(__file__).resolve().parents[1]
SOURCE = ('Types: deb deb-src\nURIs: http://azure.archive.ubuntu.com/ubuntu/\n'
          'Suites: noble noble-updates noble-backports\nComponents: main restricted universe multiverse\n'
          'Signed-By: /usr/share/keyrings/ubuntu-archive-keyring.gpg\n')


class BrowserArchiveTests(unittest.TestCase):
    """Use synthetic files only; no system repository, package manager or network is touched."""

    def test_Given_deb822_sources_When_rewriting_Then_only_exact_archive_tokens_change(self):
        """Preserve signatures, suites, disabled flags, comments, multiline URIs and other repositories."""
        source = ('# http://azure.archive.ubuntu.com/ubuntu/\n' + SOURCE +
                  '\nEnabled: no\nURIs: https://azure.archive.ubuntu.com/ubuntu\n'
                  ' https://security.ubuntu.com/ubuntu/\nSigned-By:\n inline-key-material\n')
        expected = source.replace('URIs: http://azure.archive.ubuntu.com', 'URIs: http://archive.ubuntu.com')
        expected = expected.replace('URIs: https://azure.archive.ubuntu.com', 'URIs: https://archive.ubuntu.com')
        self.assertEqual(expected, rewrite(source, True))
        self.assertEqual(expected, rewrite(expected, True))
        continuation = 'URIs: https://security.ubuntu.com/ubuntu/\r\n http://azure.archive.ubuntu.com/ubuntu\r\n'
        self.assertEqual(continuation.replace('azure.archive.', 'archive.'), rewrite(continuation, True))

    def test_Given_legacy_sources_When_rewriting_Then_options_and_comments_are_preserved(self):
        """Constrain changes to the URI after the optional apt options, without weakening signed-by."""
        source = ('# deb http://azure.archive.ubuntu.com/ubuntu noble main\n'
                  'deb [arch=amd64 signed-by=/keyring.gpg] http://azure.archive.ubuntu.com/ubuntu noble main\n'
                  'deb-src https://azure.archive.ubuntu.com/ubuntu/ noble-updates universe # retain\n'
                  'deb https://security.ubuntu.com/ubuntu noble-security main\n')
        expected = source.replace('] http://azure.archive.', '] http://archive.')
        expected = expected.replace('deb-src https://azure.archive.', 'deb-src https://archive.')
        self.assertEqual(expected, rewrite(source, False))
        self.assertEqual(expected, rewrite(expected, False))

    def test_Given_ambiguous_regional_references_When_rewriting_Then_preparation_rejects_them(self):
        """No credentials, ports, path/query substitution, lookalike host or misplaced field is accepted."""
        for uri in ['ftp://azure.archive.ubuntu.com/ubuntu', 'http://user@azure.archive.ubuntu.com/ubuntu',
                    'http://azure.archive.ubuntu.com:80/ubuntu', 'http://azure.archive.ubuntu.com/other',
                    'http://azure.archive.ubuntu.com/ubuntu?x=1', 'http://azure.archive.ubuntu.com/ubuntu#x',
                    'http://azure.archive.ubuntu.com.evil/ubuntu', 'http://AZURE.archive.ubuntu.com/ubuntu']:
            with self.subTest(uri=uri), self.assertRaises(ValueError):
                archive_uri(uri)
        for source, deb822 in [('Signed-By: http://azure.archive.ubuntu.com/ubuntu\n', True),
                               (' http://azure.archive.ubuntu.com/ubuntu\n', True),
                               ('URIs: http://azure.archive.ubuntu.com/ubuntu\n', False),
                               ('deb http://other/ubuntu azure.archive.ubuntu.com main\n', False)]:
            with self.subTest(source=source), self.assertRaises(ValueError):
                rewrite(source, deb822)

    def test_Given_multiple_files_When_one_is_invalid_Then_no_source_is_written(self):
        """Validate the full set before mutation and reject missing files or symbolic source redirection."""
        with tempfile.TemporaryDirectory() as directory, patch('prepare_browser_archive.subprocess.run') as execute:
            root = Path(directory)
            with self.assertRaises(ValueError):
                prepare(root)
            first = root / 'sources.list'
            first.write_text('deb http://azure.archive.ubuntu.com/ubuntu noble main\n')
            original = first.read_bytes()
            second = root / 'sources.list.d' / 'ubuntu.sources'
            second.parent.mkdir()
            second.write_text('URIs: http://azure.archive.ubuntu.com/other\n')
            with self.assertRaises(ValueError):
                prepare(root)
            self.assertEqual(original, first.read_bytes())
            second.unlink()
            second.symlink_to(first)
            with self.assertRaises(ValueError):
                prepare(root)
            self.assertEqual(original, first.read_bytes())
            execute.assert_not_called()

    def test_Given_changed_sources_When_refreshing_Then_failure_propagates_without_retry(self):
        """One bounded index refresh follows validated writes; an already prepared tree is idempotent."""
        with tempfile.TemporaryDirectory() as directory, patch('prepare_browser_archive.subprocess.run') as execute:
            root = Path(directory)
            source = root / 'sources.list.d' / 'ubuntu.sources'
            source.parent.mkdir()
            source.write_text(SOURCE)
            source.chmod(0o640)
            prepare(root)
            execute.assert_called_once_with(['apt-get', 'update'], check=True, timeout=120)
            self.assertEqual(SOURCE.replace('azure.archive.', 'archive.'), source.read_text())
            self.assertEqual(0o640, source.stat().st_mode & 0o777)
            prepare(root)
            self.assertEqual(1, execute.call_count)
            for failure in [subprocess.CalledProcessError(1, 'apt-get'), subprocess.TimeoutExpired('apt-get', 120)]:
                source.write_text(SOURCE)
                execute.reset_mock()
                execute.side_effect = failure
                with self.assertRaises(type(failure)):
                    prepare(root)
                self.assertEqual(1, execute.call_count)

    def test_Given_workflow_When_provisioning_Then_selection_precedes_each_first_apt_command(self):
        """All browser jobs share the same setup while existing deadlines and native installers stay intact."""
        workflow = (ROOT / '.github/workflows/verify.yml').read_text()
        jobs = dict(re.findall(r'(?ms)^  ([a-z][a-z-]*):\n(.*?)(?=^  [a-z][a-z-]*:\n|\Z)', workflow))
        selected = {name for name, body in jobs.items() if 'sudo python3 scripts/prepare_browser_archive.py' in body}
        self.assertEqual({'browser', 'contrast-experiment', 'contracts'}, selected)
        for name, minutes in [('browser', 20), ('contrast-experiment', 20), ('contracts', 15)]:
            body = jobs[name]
            self.assertIn(f'timeout-minutes: {minutes}\n', body)
            self.assertEqual(1, body.count('sudo python3 scripts/prepare_browser_archive.py'))
            setup = body.index('sudo python3 scripts/prepare_browser_archive.py')
            first = min(match.start() for match in re.finditer(r'apt-get install|dotnet run --project tests/tooling/IdentityPlatform.BrowserInstaller', body))
            self.assertLess(setup, first)
            self.assertIn('--no-build -- install --with-deps ', body)
        self.assertIn('python3 scripts/test_prepare_browser_archive.py', jobs['supply-chain'])
        with patch('prepare_browser_archive.platform.freedesktop_os_release', return_value={'ID': 'debian', 'VERSION_ID': '12'}), patch('prepare_browser_archive.prepare') as execute:
            with self.assertRaises(ValueError):
                main()
            execute.assert_not_called()


if __name__ == '__main__':
    run(6)
