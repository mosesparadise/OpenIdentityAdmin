"""Pure owned-process controls; these never start browsers or containers."""
import os
import json
import ctypes
from pathlib import Path
import subprocess
import sys
import tempfile
import threading
import time
import unittest
from unittest.mock import patch
import process as owned
from process import run, reconcile


class ProcessTests(unittest.TestCase):
    def test_pass_and_failure_exits_remain_distinct(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory)
            for code in (0, 2, 7):
                journal = path / str(code)
                self.assertEqual(run([sys.executable, '-c', f'raise SystemExit({code})'], os.environ.copy(), path, journal, 2), code)
                self.assertTrue(reconcile(journal))

    def test_timeout_cleans_owned_process(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory); journal = path / 'process.json'
            with self.assertRaises(subprocess.TimeoutExpired):
                run([sys.executable, '-c', 'import time; time.sleep(30)'], os.environ.copy(), path, journal, 0.1)
            self.assertTrue(reconcile(journal))

    def test_missing_journal_is_incomplete(self):
        with tempfile.TemporaryDirectory() as directory:
            self.assertFalse(reconcile(Path(directory) / 'missing.json'))

    def test_malformed_identity_cannot_target_any_group(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'process.json'
            for value in ({}, [], {'pid': 0, 'start_ticks': 1, 'complete': False},
                          {'pid': -1, 'start_ticks': 1, 'complete': False},
                          {'pid': True, 'start_ticks': 1, 'complete': False}):
                path.write_text(json.dumps(value))
                self.assertFalse(reconcile(path))

    def test_journal_failure_cleans_already_started_child(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory); children = []; original = subprocess.Popen
            def spawn(*args, **kwargs):
                child = original(*args, **kwargs); children.append(child); return child
            with patch.object(owned.subprocess, 'Popen', side_effect=spawn), \
                 patch.object(owned, 'write', side_effect=OSError('synthetic journal failure')):
                with self.assertRaises(OSError):
                    run([sys.executable, '-c', 'import time; time.sleep(30)'], os.environ.copy(), path, path/'journal', 1)
            self.assertEqual(len(children), 1)
            self.assertIsNotNone(children[0].poll())
            self.assertFalse(owned.group_alive(children[0].pid))

    def test_descendant_is_terminated_after_leader_exits(self):
        # Adopt the orphan only in this pure test process so it can be reaped deterministically.
        libc = ctypes.CDLL(None)
        self.assertEqual(libc.prctl(36, 1, 0, 0, 0), 0)
        try:
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory); child_file = path / 'child'; reaped = []
                def reap():
                    end = time.monotonic() + 5
                    while time.monotonic() < end:
                        if child_file.exists():
                            try:
                                pid = int(child_file.read_text())
                                reaped.append(os.waitpid(pid, 0)); return
                            except (ChildProcessError, ValueError): pass
                        time.sleep(0.01)
                worker = threading.Thread(target=reap, daemon=True); worker.start()
                code = ('import os,time,pathlib; pid=os.fork(); '
                        'time.sleep(30) if pid==0 else pathlib.Path("child").write_text(str(pid))')
                self.assertEqual(run([sys.executable, '-c', code], os.environ.copy(), path, path/'journal', 2), 0)
                worker.join(6)
                self.assertFalse(worker.is_alive()); self.assertEqual(len(reaped), 1)
                self.assertTrue(os.WIFSIGNALED(reaped[0][1]))
                self.assertTrue(reconcile(path/'journal'))
        finally:
            libc.prctl(36, 0, 0, 0, 0)


if __name__ == '__main__': unittest.main()
