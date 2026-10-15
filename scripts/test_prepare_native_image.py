"""Seed image-preparation failures without contacting Docker or a registry."""
import json
import re
import subprocess
import unittest
from unittest.mock import patch
from control_runner import run
from prepare_native_image import ROOT, SETUPS, image_reference, prepare

PG = '74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336'
SQL = '4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090'


class NativeImagePreparationTests(unittest.TestCase):
    """Keep exact source pins, acquisition bounds and rejection behavior independent."""

    def test_Given_current_setup_When_resolving_Then_exact_provider_pins_are_used(self):
        """Existing immutable images remain authoritative for both providers."""
        self.assertEqual(f'docker.io/library/postgres:18@sha256:{PG}', image_reference('PostgreSql', SETUPS['PostgreSql'].read_text()))
        self.assertEqual(f'mcr.microsoft.com/mssql/server:2022-latest@sha256:{SQL}', image_reference('SqlServer', SETUPS['SqlServer'].read_text()))

    def test_Given_missing_changed_or_ambiguous_pin_When_resolving_Then_preparation_fails(self):
        """No tag-only fallback, guessed digest, duplicate declaration or override is allowed."""
        source = SETUPS['PostgreSql'].read_text()
        for invalid in ('', source + source, source.replace(PG, 'bad'), source + '\ndatabase.WithImageRegistry("other");', source.replace('.WithImageTag(ImageTag)', '.WithImage("other")'),
                        source.replace('.WithImageSHA256(ImageDigest);', '.WithImageSHA256(ImageDigest).WithImage("other");')):
            with self.subTest(source=invalid[:20]), self.assertRaises(ValueError):
                image_reference('PostgreSql', invalid)
        with self.assertRaises(KeyError):
            image_reference('unit', source)

    def test_Given_individually_invalid_constants_When_resolving_Then_preparation_fails(self):
        """Each adapter constant must be present exactly once with its reviewed literal shape."""
        for provider, path in SETUPS.items():
            source = path.read_text()
            for name in ('DatabaseName', 'ImageTag', 'ImageDigest'):
                declaration = re.search(rf'const string {name} = "[^"]+";', source).group(0)
                for invalid in (source.replace(declaration, ''), source + '\n' + declaration):
                    with self.subTest(provider=provider, constant=name), self.assertRaises(ValueError):
                        image_reference(provider, invalid)
            with self.assertRaises(ValueError):
                image_reference(provider, source.replace('DatabaseName = "database"', 'DatabaseName = "other"'))

    def test_Given_exact_inspected_digest_When_preparing_Then_single_bounded_pull_precedes_inspection(self):
        """Canonical Docker namespace forms pass; commands never create or start containers."""
        for provider, repository, digest in [('PostgreSql', 'postgres', PG), ('PostgreSql', 'docker.io/library/postgres', PG),
                                             ('SqlServer', 'mcr.microsoft.com/mssql/server', SQL)]:
            with self.subTest(provider=provider), patch('prepare_native_image.subprocess.run') as execute:
                execute.return_value.stdout = json.dumps([repository + '@sha256:' + digest])
                prepare(provider, SETUPS[provider].read_text())
                self.assertEqual(2, execute.call_count)
                self.assertEqual(['docker', 'pull', image_reference(provider, SETUPS[provider].read_text())], execute.call_args_list[0].args[0])
                self.assertEqual({'check': True, 'timeout': 300}, execute.call_args_list[0].kwargs)
                self.assertEqual(15, execute.call_args_list[1].kwargs['timeout'])
                self.assertTrue(execute.call_args_list[1].kwargs['check'])

    def test_Given_pull_failure_or_timeout_When_preparing_Then_no_inspection_or_retry_occurs(self):
        """A failed prerequisite cannot proceed to verification or consume repeated pull windows."""
        for error in [subprocess.CalledProcessError(1, 'docker'), subprocess.TimeoutExpired('docker', 300), FileNotFoundError()]:
            with self.subTest(error=type(error)), patch('prepare_native_image.subprocess.run', side_effect=error) as execute:
                with self.assertRaises(type(error)):
                    prepare('PostgreSql', SETUPS['PostgreSql'].read_text())
                self.assertEqual(1, execute.call_count)

    def test_Given_inspection_failure_or_mismatched_digest_When_preparing_Then_preparation_fails(self):
        """Tag-only, wrong repository/digest, malformed and unavailable receipts fail closed."""
        for receipt in ['[]', 'null', '{}', 'broken', '[null]', json.dumps(['postgres:18']),
                        json.dumps(['other@sha256:' + PG]), json.dumps(['postgres@sha256:' + SQL])]:
            with self.subTest(receipt=receipt), patch('prepare_native_image.subprocess.run') as execute:
                execute.return_value.stdout = receipt
                with self.assertRaises(ValueError):
                    prepare('PostgreSql', SETUPS['PostgreSql'].read_text())
                self.assertEqual(2, execute.call_count)
        for error in [subprocess.CalledProcessError(1, 'docker'), subprocess.TimeoutExpired('docker', 15)]:
            with patch('prepare_native_image.subprocess.run', side_effect=[None, error]) as execute:
                with self.assertRaises(type(error)):
                    prepare('PostgreSql', SETUPS['PostgreSql'].read_text())
                self.assertEqual(2, execute.call_count)

    def test_Given_workflow_When_preparing_Then_every_native_job_prepares_before_execution(self):
        """Keep preparation mandatory, before history probes too, and outside unit/contracts."""
        workflow = (ROOT / '.github/workflows/verify.yml').read_text()
        jobs = dict(re.findall(r'(?ms)^  ([a-z][a-z-]*):\n(.*?)(?=^  [a-z][a-z-]*:\n|\Z)', workflow))
        expected = {'verify': 20, 'browser': 20, 'mutations': 30, 'administrator-mutations': 30, 'governance-mutations': 30}
        selected = {name for name, body in jobs.items() if 'scripts/prepare_native_image.py' in body}
        self.assertEqual(set(expected), selected)
        for job, minutes in expected.items():
            body = jobs[job]
            self.assertEqual(1, body.count('scripts/prepare_native_image.py'))
            self.assertIn(f'timeout-minutes: {minutes}\n', body)
            prepare_at = body.index('scripts/prepare_native_image.py')
            producers = [match.start() for match in re.finditer(r'python3 scripts/verify(?:_mutations|_history_experiment)?\.py', body)]
            self.assertTrue(producers)
            self.assertTrue(all(prepare_at < producer for producer in producers))
        self.assertIn("if: matrix.profile != 'unit'\n        run: python3 scripts/prepare_native_image.py ${{ matrix.profile }}", jobs['verify'])
        self.assertIn('python3 scripts/test_prepare_native_image.py', jobs['supply-chain'])


if __name__ == '__main__':
    run(7)
