import tempfile
import os
import signal
import subprocess
import sys
import time
import unittest
from pathlib import Path
from unittest.mock import patch
import xml.etree.ElementTree as ET

from verify_mutations import changed_source, execute, validate_report, manifest, run, require_clean_checkout, ROOT

NAME = 'IdentityPlatform.Issuer.Tests.Features.Protocol.DecisionTests.Denies(provider: "PostgreSql")'
METHOD = NAME.split('(')[0]
MARKER = 'Critical decision bypass: seeded-control.'


def report(*, failed=True):
    root = ET.Element('TestRun')
    results = ET.SubElement(root, 'Results')
    result = ET.SubElement(results, 'UnitTestResult', testName=NAME, outcome='Failed' if failed else 'Passed')
    summary = ET.SubElement(root, 'ResultSummary', outcome='Failed' if failed else 'Completed')
    ET.SubElement(summary, 'Counters', total='1', executed='1', passed='0' if failed else '1', failed='1' if failed else '0', notExecuted='0')
    if failed:
        error = ET.SubElement(ET.SubElement(result, 'Output'), 'ErrorInfo')
        ET.SubElement(error, 'Message').text = MARKER
        ET.SubElement(error, 'StackTrace').text = 'at ' + METHOD + '(String provider)'
        info = ET.SubElement(ET.SubElement(summary, 'RunInfos'), 'RunInfo', outcome='Error')
        ET.SubElement(info, 'Text').text = '[xUnit.net 00:00:01.20]     ' + NAME + ' [FAIL]'
    return root


def clean_result(result):
    return result.wasSuccessful() and result.testsRun > 0 and not result.skipped and not result.expectedFailures


def terminated(pid):
    try:
        return Path(f'/proc/{pid}/stat').read_text().split()[2] == 'Z'
    except (FileNotFoundError, ProcessLookupError):
        return True  # The OS may reap the child between observations.


class MutationEvidenceTests(unittest.TestCase):
    def check(self, root, *, code=2, failed=True):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'result.trx'
            ET.ElementTree(root).write(path)
            return validate_report(path, {NAME}, code, marker=MARKER if failed else None, method=METHOD if failed else None)

    def test_expected_behavioral_failure_and_baseline(self):
        self.assertEqual(1, self.check(report())['failed'])
        self.assertEqual(1, self.check(report(failed=False), code=0, failed=False)['passed'])

    def test_survivor_is_not_killed(self):
        with self.assertRaises(ValueError):
            self.check(report(failed=False), code=0)

    def test_wrong_exit_codes_never_count(self):
        for code in (0, 1, 3, 4, 8, 137):
            with self.subTest(code=code), self.assertRaises(ValueError):
                self.check(report(), code=code)

    def test_setup_cleanup_status_and_malformed_success_are_not_expected_kills(self):
        for message in ('Setup unavailable', 'Baseline state could not be restored.', 'Expected BadRequest, actual ServiceUnavailable',
                        'Unexpected success did not contain a JSON token response.', MARKER + '\nCleanup failed'):
            value = report()
            value.find('.//Message').text = message
            with self.subTest(message=message), self.assertRaises(ValueError):
                self.check(value)

    def test_marker_at_wrong_assertion_or_only_in_stack_is_not_a_kill(self):
        value = report()
        value.find('.//StackTrace').text = 'at Different.Test()'
        with self.assertRaises(ValueError):
            self.check(value)
        value.find('.//StackTrace').text = METHOD + MARKER
        value.find('.//Message').text = 'Another failure'
        with self.assertRaises(ValueError):
            self.check(value)

    def test_skipped_aborted_and_incomplete_counters_fail(self):
        for key in ('notExecuted', 'aborted', 'error', 'timeout', 'passedButRunAborted'):
            value = report()
            value.find('.//Counters').set(key, '1')
            with self.subTest(key=key), self.assertRaises(ValueError):
                self.check(value)

    def test_duplicate_omitted_substituted_identities_fail(self):
        for mode in ('duplicate', 'omitted', 'substituted'):
            value = report()
            results = value.find('Results')
            if mode == 'duplicate':
                results.append(ET.fromstring(ET.tostring(results[0])))
            elif mode == 'omitted':
                results.remove(results[0])
            else:
                results[0].set('testName', 'Different.Test')
            with self.subTest(mode=mode), self.assertRaises(ValueError):
                self.check(value)

    def test_expected_kill_plus_fixture_cleanup_error_fails(self):
        value = report()
        info = ET.SubElement(value.find('.//RunInfos'), 'RunInfo', outcome='Error')
        ET.SubElement(info, 'Text').text = 'Fixture cleanup failed'
        with self.assertRaises(ValueError):
            self.check(value)

    def test_replacing_failure_echo_with_cleanup_error_fails(self):
        value = report()
        value.find('.//RunInfo/Text').text = 'Fixture cleanup failed'
        with self.assertRaises(ValueError):
            self.check(value)

    def test_run_summary_error_cannot_hide_behind_expected_kill(self):
        value = report()
        ET.SubElement(ET.SubElement(value.find('ResultSummary'), 'Output'), 'ErrorInfo')
        with self.assertRaises(ValueError):
            self.check(value)

    def test_incomplete_or_unknown_result_fails(self):
        for outcome in ('NotExecuted', 'Timeout', 'Inconclusive'):
            value = report()
            value.find('.//UnitTestResult').set('outcome', outcome)
            with self.subTest(outcome=outcome), self.assertRaises(ValueError):
                self.check(value)

    def test_aborted_summary_and_missing_counters_fail(self):
        value = report()
        value.find('ResultSummary').set('outcome', 'Aborted')
        with self.assertRaises(ValueError):
            self.check(value)

    def test_each_required_counter_must_be_present_even_when_zero(self):
        for failed in (True, False):
            for key in ('total', 'executed', 'passed', 'failed'):
                value = report(failed=failed)
                del value.find('.//Counters').attrib[key]
                with self.subTest(failed=failed, key=key), self.assertRaises(ValueError):
                    self.check(value, code=2 if failed else 0, failed=failed)

    def test_duplicate_summary_counters_or_error_info_fail(self):
        for tag in ('ResultSummary', 'Counters', 'ErrorInfo', 'Message', 'StackTrace'):
            value = report()
            node = value.find('.//' + tag)
            parent = next(parent for parent in value.iter() if node in list(parent))
            parent.append(ET.fromstring(ET.tostring(node)))
            with self.subTest(tag=tag), self.assertRaises(ValueError):
                self.check(value)
        value = report()
        value.find('ResultSummary').remove(value.find('.//Counters'))
        with self.assertRaises(ValueError):
            self.check(value)

    def test_missing_report_fails(self):
        with tempfile.TemporaryDirectory() as directory, self.assertRaises(FileNotFoundError):
            validate_report(Path(directory) / 'absent.trx', {NAME}, 2, marker=MARKER, method=METHOD)

    def test_stale_report_is_removed_before_a_failed_process(self):
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'result.trx'
            ET.ElementTree(report()).write(path)
            with patch('verify_mutations.run', return_value=(2, '')), self.assertRaises(FileNotFoundError):
                execute('selection', {NAME}, Path(directory), {}, item={'marker': MARKER, 'method': METHOD})

    def test_source_restored_after_normal_failure_and_cancellation(self):
        for error in (None, RuntimeError('seeded failure'), KeyboardInterrupt(), SystemExit(143)):
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'source.cs'
                original = b'prefix\r\noriginal\r\nsuffix'
                path.write_bytes(original)
                try:
                    with changed_source(path, 'original', 'mutant'):
                        self.assertIn(b'mutant', path.read_bytes())
                        if error:
                            raise error
                except BaseException as actual:
                    self.assertIs(error, actual)
                self.assertEqual(original, path.read_bytes())

    def test_source_drift_or_ambiguous_replacement_never_mutates(self):
        for original in (b'no match', b'original original'):
            with tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'source.cs'
                path.write_bytes(original)
                with self.assertRaises(ValueError), changed_source(path, 'original', 'mutant'):
                    self.fail('Ambiguous mutation was entered')
                self.assertEqual(original, path.read_bytes())

    def test_reviewed_manifest_targets_exist_exactly_once(self):
        self.assertEqual(7, len(manifest(ROOT)))

    def test_exited_parent_pipe_holder_and_stubborn_child_are_terminated(self):
        for stubborn, redirected in ((False, False), (True, False), (True, True)):
            with self.subTest(stubborn=stubborn, redirected=redirected), tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'child.pid'
                program = '''import os,signal,time,sys
from pathlib import Path
if os.fork():
    if sys.argv[3] == 'True':
        time.sleep(60)
    os._exit(0)
if sys.argv[2] == 'True':
    signal.signal(signal.SIGTERM, signal.SIG_IGN)
if sys.argv[3] == 'True':
    fd = os.open(os.devnull, os.O_WRONLY)
    os.dup2(fd, 1)
    os.dup2(fd, 2)
    os.close(fd)
Path(sys.argv[1]).write_text(str(os.getpid()))
time.sleep(60)
'''
                started = time.monotonic()
                try:
                    with self.assertRaises(subprocess.TimeoutExpired):
                        run([sys.executable, '-c', program, str(path), str(stubborn), str(redirected)], 0.5, os.environ.copy(), cleanup_timeout=0.5)
                    self.assertLess(time.monotonic() - started, 4)
                    pid = int(path.read_text())
                    deadline = time.monotonic() + 1
                    while not terminated(pid) and time.monotonic() < deadline:
                        time.sleep(0.01)  # Observe signal delivery; never rerun the command being verified.
                    self.assertTrue(terminated(pid))
                finally:
                    if path.exists():
                        try:
                            os.kill(int(path.read_text()), signal.SIGKILL)
                        except ProcessLookupError:
                            pass


    def test_dirty_tracked_or_untracked_inputs_cannot_claim_commit_identity(self):
        for code, status in ((0, ' M tests/changed.cs'), (0, '?? eng/new.json'), (1, '')):
            with patch('verify_mutations.run', return_value=(code, status)), self.assertRaises(ValueError):
                require_clean_checkout({})
        with patch('verify_mutations.run', return_value=(0, '')):
            require_clean_checkout({})

    def test_reaped_child_is_a_completed_termination(self):
        for error in (FileNotFoundError(), ProcessLookupError()):
            with patch.object(Path, 'read_text', side_effect=error):
                self.assertTrue(terminated(123))

    def test_python_gate_rejects_empty_skipped_expected_failures_and_errors(self):
        result = unittest.TestResult()
        self.assertFalse(clean_result(result))
        result.testsRun = 1
        self.assertTrue(clean_result(result))
        for field in ('skipped', 'expectedFailures', 'errors', 'failures', 'unexpectedSuccesses'):
            setattr(result, field, ['seeded'])
            self.assertFalse(clean_result(result))
            setattr(result, field, [])


if __name__ == '__main__':
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(MutationEvidenceTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    sys.exit(0 if clean_result(result) else 1)
