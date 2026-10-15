"""Fault controls ensure a failed check cannot prevent independent driver restoration."""
import hashlib
import json
from pathlib import Path
import tempfile
import unittest
from unittest.mock import patch
import cleanup


class CleanupTests(unittest.TestCase):
    def exercise(self, source_failure=False, process_failure=False, docker_failure=False, journal_failure=False, overlay_failure=False):
        with tempfile.TemporaryDirectory() as directory:
            work = Path(directory); (work / 'private').mkdir()
            original = work / 'original'; library = original / 'package/lib'; library.mkdir(parents=True)
            (library / 'coreBundle.js').write_bytes(b'original')
            target = work / 'driver/.playwright/package/lib'; target.mkdir(parents=True)
            (target / 'coreBundle.js').write_bytes(b'patched')
            (target / 'identity-webkit-observer.cjs').write_bytes(b'observer')
            (work / 'source-before.json').write_text('{}')
            (work / 'private/driver.json').write_text(json.dumps({'original': str(original)}))
            (work / 'private/docker-baseline.json').write_text('{"containers":[],"volumes":[]}')
            with patch.object(cleanup, 'ORIGINAL', hashlib.sha256(b'original').hexdigest()), \
                 patch.object(cleanup.overlay, 'restore', return_value=not overlay_failure), \
                 patch.object(cleanup, 'source_hashes', side_effect=ValueError() if source_failure else None, return_value={}), \
                 patch.object(cleanup, 'reconcile', return_value=not process_failure,
                              side_effect=[ValueError('truncated journal'), True] if journal_failure else None) as reconcile, \
                 patch.object(cleanup, 'command', side_effect=RuntimeError() if docker_failure else None, return_value=''):
                status = cleanup.cleanup(work, work)
            if journal_failure: self.assertEqual(reconcile.call_count, 2)
            self.assertEqual((target / 'coreBundle.js').read_bytes(), b'original')
            self.assertFalse((target / 'identity-webkit-observer.cjs').exists())
            return status, json.loads((work / 'public/cleanup.json').read_text())

    def test_complete_cleanup(self):
        status, result = self.exercise()
        self.assertEqual(status, 0); self.assertTrue(result['complete'])

    def test_source_failure_still_restores_driver(self):
        status, result = self.exercise(source_failure=True)
        self.assertEqual(status, 1); self.assertTrue(result['driver_restored']); self.assertFalse(result['source_unchanged'])

    def test_process_failure_still_restores_driver(self):
        status, result = self.exercise(process_failure=True)
        self.assertEqual(status, 1); self.assertTrue(result['driver_restored']); self.assertFalse(result['processes_clean'])

    def test_docker_failure_keeps_cleanup_incomplete(self):
        status, result = self.exercise(docker_failure=True)
        self.assertEqual(status, 1); self.assertFalse(result['docker_clean'])

    def test_malformed_collector_journal_still_reconciles_native_group(self):
        status, result = self.exercise(journal_failure=True)
        self.assertEqual(status, 1); self.assertFalse(result['processes_clean'])
        self.assertTrue(result['driver_restored'])

    def test_instrumented_copy_failure_still_restores_driver(self):
        status, result = self.exercise(overlay_failure=True)
        self.assertEqual(status, 1); self.assertFalse(result['instrumented_source_restored'])
        self.assertTrue(result['driver_restored'])


if __name__ == '__main__': unittest.main()
