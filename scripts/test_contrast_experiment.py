"""Reject empty/substituted selection and partial cohorts, even when the runner exits successfully."""
import tempfile
import unittest
import os
import signal
import subprocess
import sys
import time
from pathlib import Path
from xml.sax.saxutils import escape
from verify_contrast_experiment import TEST, MARKER, SAMPLES, execute, require_completion, require_selection


class ContrastExperimentEvidenceTests(unittest.TestCase):
    def test_only_exact_discovered_case_is_allowed(self):
        correct = {'a': (TEST + '(provider: "PostgreSql")', 'PostgreSql', True)}
        other = {'b': ('Different case', 'PostgreSql', True)}
        require_selection(correct | other, correct)
        for selected in ({}, other, correct | other, {'x': next(iter(correct.values()))}):
            with self.assertRaises(ValueError):
                require_selection(correct | other, selected)
        with self.assertRaises(ValueError):
            require_selection(other, other)

    def test_complete_cohort_and_marker_are_both_required(self):
        arms = ('FullWrapper', 'OwnedWrapper')
        lines = [f'Boundary sample {sample}/{SAMPLES}: {arm} observed finding=false' for sample in range(1, SAMPLES + 1) for arm in arms]
        with tempfile.TemporaryDirectory() as directory:
            report = Path(directory) / 'contrast.trx'

            def check(values, *, summary='Completed', result='Passed', extra='', counters='total="1" executed="1" passed="1" failed="0"'):
                report.write_text(f'<TestRun><Results><UnitTestResult outcome="{result}"><Output><StdOut>' + escape('\n'.join(values)) +
                                  '</StdOut></Output></UnitTestResult></Results>' +
                                  f'<ResultSummary outcome="{summary}"><Counters {counters}/></ResultSummary>{extra}</TestRun>')
                require_completion(report)

            check(lines + [MARKER])
            for invalid in ([], lines, lines[:-1] + [MARKER], lines + [MARKER, MARKER],
                            lines[:-1] + [lines[0], MARKER], lines + ['Boundary sample 21/20: FullWrapper observed finding=false', MARKER],
                            [lines[0].replace('finding=false', 'finding=true')] + lines[1:] + [MARKER],
                            lines + [MARKER.replace('findings=0', 'findings=1')]):
                with self.assertRaises(ValueError):
                    check(invalid)
            for change in ({'summary': 'Failed'}, {'result': 'Failed'}, {'extra': '<RunInfo/>'}, {'extra': '<ErrorInfo/>'},
                           {'counters': 'total="1" executed="1" passed="1" failed="1"'},
                           {'counters': 'total="1" executed="1" passed="1" failed="0" notExecuted="1"'},
                           {'extra': '<ResultSummary outcome="Completed"/>'}):
                with self.assertRaises(ValueError):
                    check(lines + [MARKER], **change)

    def test_timeout_and_exited_parent_cleanup_terminate_stubborn_descendant(self):
        for parent_exits in (False, True):
            with self.subTest(parent_exits=parent_exits), tempfile.TemporaryDirectory() as directory:
                pidfile = Path(directory) / 'child.pid'
                program = '''import os, signal, sys, time
from pathlib import Path
if os.fork():
    while not Path(sys.argv[1]).exists(): time.sleep(0.01)
    if sys.argv[2] == 'True': os._exit(0)
else:
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
    Path(sys.argv[1]).write_text(str(os.getpid()))
time.sleep(60)
'''
                started = time.monotonic()
                try:
                    command = [sys.executable, '-c', program, str(pidfile), str(parent_exits)]
                    if parent_exits:
                        execute(command, os.environ.copy(), timeout=1, cleanup_timeout=0.2)
                    else:
                        with self.assertRaisesRegex(RuntimeError, 'deadline'):
                            execute(command, os.environ.copy(), timeout=1, cleanup_timeout=0.2)
                    self.assertLess(time.monotonic() - started, 3)
                    self.require_terminated(int(pidfile.read_text()))
                finally:
                    self.kill_recorded(pidfile)

    def test_sigterm_unwinds_runner_cleanup(self):
        with tempfile.TemporaryDirectory() as directory:
            pidfile = Path(directory) / 'child.pid'
            runner = '''import os, sys
from verify_contrast_experiment import execute, install_cancellation_handler
install_cancellation_handler()
execute([sys.executable, '-c', "import os,signal,sys,time; from pathlib import Path; signal.signal(signal.SIGTERM, signal.SIG_IGN); Path(sys.argv[1]).write_text(str(os.getpid())); time.sleep(60)", sys.argv[1]], os.environ.copy(), timeout=5, cleanup_timeout=0.2)
'''
            environment = os.environ.copy()
            environment['PYTHONPATH'] = str(Path(__file__).resolve().parent)
            process = subprocess.Popen([sys.executable, '-c', runner, str(pidfile)], env=environment)
            try:
                deadline = time.monotonic() + 3
                while not pidfile.exists() and time.monotonic() < deadline:
                    time.sleep(0.01)
                self.assertTrue(pidfile.exists())
                process.send_signal(signal.SIGTERM)
                self.assertEqual(143, process.wait(timeout=3))
                self.require_terminated(int(pidfile.read_text()))
            finally:
                if process.poll() is None:
                    process.kill()
                process.wait(timeout=1)
                self.kill_recorded(pidfile)

    def test_discovery_pipe_holder_cannot_defeat_timeout_after_parent_exits(self):
        with tempfile.TemporaryDirectory() as directory:
            pidfile = Path(directory) / 'child.pid'
            program = '''import os, signal, sys, time
from pathlib import Path
if os.fork(): os._exit(0)
signal.signal(signal.SIGTERM, signal.SIG_IGN)
Path(sys.argv[1]).write_text(str(os.getpid()))
time.sleep(60)
'''
            started = time.monotonic()
            try:
                with self.assertRaisesRegex(RuntimeError, 'deadline'):
                    execute([sys.executable, '-c', program, str(pidfile)], os.environ.copy(), timeout=1, cleanup_timeout=0.2, capture=True)
                self.assertLess(time.monotonic() - started, 3)
                self.require_terminated(int(pidfile.read_text()))
                self.assertEqual('discovery control\n', execute([sys.executable, '-c', "print('discovery control')"], os.environ.copy(), capture=True))
            finally:
                self.kill_recorded(pidfile)

    def require_terminated(self, pid):
        deadline = time.monotonic() + 1
        while time.monotonic() < deadline:
            try:
                if Path(f'/proc/{pid}/stat').read_text().split(') ', 1)[1].startswith('Z'):
                    return
            except (FileNotFoundError, ProcessLookupError):
                return
            time.sleep(0.01)
        self.fail('The experiment left a live descendant')

    @staticmethod
    def kill_recorded(path):
        if path.exists():
            try:
                os.kill(int(path.read_text()), signal.SIGKILL)
            except ProcessLookupError:
                pass


if __name__ == '__main__':
    unittest.main()
