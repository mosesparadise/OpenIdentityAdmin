import io
import json
from contextlib import redirect_stdout
import tempfile
import os
import signal
import subprocess
import sys
import time
import unittest
from pathlib import Path
from unittest.mock import Mock, patch
import verify_mutations as verifier
import xml.etree.ElementTree as ET

from verify_mutations import changed_source, execute, validate_report, manifest, run, require_clean_checkout, ROOT, FAILURE_DIAGNOSTIC

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
        ET.SubElement(info, 'Text').text = FAILURE_DIAGNOSTIC
    return root


def clean_result(result):
    return result.wasSuccessful() and result.testsRun > 0 and not result.skipped and not result.expectedFailures


def terminated(pid):
    try:
        record = Path(f'/proc/{pid}/stat').read_text().rsplit(') ', 1)
        fields = record[1].split() if len(record) == 2 else []
        return bool(fields) and fields[0] in ('Z', 'X')
    except (FileNotFoundError, ProcessLookupError):
        return True  # The OS may reap the child between observations.


def wait_for_termination(pid, timeout=1):
    deadline = time.monotonic() + timeout
    while True:
        if terminated(pid):
            return True  # Preserve the terminal observation; the PID can subsequently disappear/recycle.
        if time.monotonic() >= deadline:
            return False
        time.sleep(0.01)


def diagnostic_run(*, error=None, cleanup_error=None, code=0, sink_failure=None):
    """Inject subprocess outcomes and return only captured diagnostics plus the supervised mock."""
    secret = 'synthetic-phase-secret'
    process = Mock(pid=123, returncode=code)
    process.poll.return_value = code
    process.communicate.side_effect = [error, ('', None), ('', None)] if error else [(secret, None)]
    process.wait.side_effect = cleanup_error
    output = io.StringIO()
    original_print = print
    def emit(*args, **kwargs):
        if sink_failure and '"event": "' + sink_failure + '"' in args[0]:
            raise BrokenPipeError(secret)
        original_print(*args, **kwargs)
    with patch.object(verifier.subprocess, 'Popen', return_value=process), patch.object(verifier.os, 'killpg') as killpg, \
            patch('builtins.print', side_effect=emit), redirect_stdout(output):
        try:
            verifier.perform('baseline', 'execution', run, [secret], 180, {'private': secret})
        except BaseException as observed:
            assert observed is (cleanup_error or error)
        else:
            assert error is None and cleanup_error is None, 'Expected supervised failure did not propagate'
    assert [call.args for call in killpg.call_args_list] == ([(123, signal.SIGTERM), (123, signal.SIGKILL)] if error else [])
    text = output.getvalue()
    assert secret not in text
    events = [json.loads(line.removeprefix('mutation-diagnostic ')) for line in text.splitlines()]
    return events, process


def incomplete_main(root, role):
    """Exercise actual orchestration/restoration with a timeout in each evidence-producing role."""
    source = root / 'target.cs'
    source.write_text('original')
    item = {'id': 'control', 'path': 'target.cs', 'before': 'original', 'after': 'mutated', 'method': METHOD, 'marker': MARKER}
    evidence = root / 'artifacts/mutations/PostgreSql/evidence.json'
    evidence.parent.mkdir(parents=True)
    evidence.write_text('stale')
    def execute_phase(*args, **kwargs):
        if verifier.PHASE.get()[0] == role:
            raise subprocess.TimeoutExpired('synthetic-phase-secret', 180)
        return {'total': 1}
    with patch.object(verifier, 'ROOT', root), patch.object(verifier, 'manifest', return_value=[item]), \
            patch.object(verifier, 'require_clean_checkout'), patch.object(verifier, 'run', return_value=(0, 'f' * 40)), \
            patch.object(verifier, 'build'), patch.object(verifier, 'discover', return_value={'uid': (NAME, 'PostgreSql', False)}), \
            patch.object(verifier, 'execute', side_effect=execute_phase), patch.object(verifier.signal, 'signal'), \
            patch.object(sys, 'argv', ['verify_mutations', 'PostgreSql', '--disposable-checkout']), redirect_stdout(io.StringIO()):
        try:
            verifier.main()
        except subprocess.TimeoutExpired:
            pass
        else:
            raise AssertionError('Incomplete orchestration unexpectedly returned')
    assert source.read_text() == 'original' and not evidence.exists()


class MutationEvidenceTests(unittest.TestCase):
    def test_Given_CommandOutcomes_When_DiagnosticsAreEmitted_Then_NoExitGrantsAcceptance(self):
        """Zero, expected test-failure and other exits retain distinct nonaccepting observations."""
        for code, outcome in [(0, 'zero-exit'), (2, 'test-failure-exit'), (3, 'other-exit')]:
            with self.subTest(code=code):
                events, process = diagnostic_run(code=code)
                self.assertEqual(['started', 'returned'], [event['event'] for event in events])
                self.assertEqual(outcome, events[-1]['outcome'])
                self.assertEqual(('baseline', 'execution', 0), tuple(events[-1][key] for key in ('role', 'operation', 'ordinal')))
                self.assertEqual((180, 10), (events[-1]['budget_seconds'], events[-1]['cleanup_budget_seconds']))
                self.assertTrue(all(type(event['elapsed_ms']) is int and event['elapsed_ms'] >= 0 for event in events))
                process.communicate.assert_called_once_with(timeout=180)
                process.wait.assert_not_called()

    def test_Given_FailedCommand_When_CleanupRuns_Then_PrimaryFailureAndCleanupAreDistinct(self):
        """Secret-bearing timeouts/cancellation/errors stay fatal without claiming complete cleanup."""
        secret = 'synthetic-phase-secret'
        for error, outcome in [(subprocess.TimeoutExpired(secret, 180, secret, secret), 'timeout'),
                               (RuntimeError(secret), 'error'), (KeyboardInterrupt(secret), 'cancelled')]:
            for cleanup_error in (None, subprocess.TimeoutExpired(secret, 10, secret, secret)):
                with self.subTest(outcome=outcome, incomplete=cleanup_error is not None):
                    events, process = diagnostic_run(error=error, cleanup_error=cleanup_error)
                    self.assertEqual(['started', 'command-failed', 'cleanup-incomplete' if cleanup_error else 'cleanup-returned'],
                                     [event['event'] for event in events])
                    self.assertEqual(outcome, events[1]['outcome'])
                    self.assertEqual('timeout' if cleanup_error else 'returned', events[-1]['outcome'])
                    self.assertEqual([180, 10, 10], [call.kwargs['timeout'] for call in process.communicate.call_args_list])
                    process.wait.assert_called_once_with(timeout=10)

    def test_Given_LaunchFailure_When_CommandCannotStart_Then_NoCleanupOrSecretsAreInvented(self):
        """A launch exception has its own safe event and does not imply an existing child."""
        secret = 'synthetic-phase-secret'
        output = io.StringIO()
        with patch.object(verifier.subprocess, 'Popen', side_effect=OSError(secret)), \
                patch.object(verifier, 'cleanup_process') as cleanup, redirect_stdout(output):
            with self.assertRaises(OSError):
                verifier.perform('baseline', 'build', run, [secret], 180, {'private': secret})
        self.assertNotIn(secret, output.getvalue())
        events = [json.loads(line.removeprefix('mutation-diagnostic ')) for line in output.getvalue().splitlines()]
        self.assertEqual(['started', 'launch-failed'], [event['event'] for event in events])
        self.assertEqual('error', events[-1]['outcome'])
        cleanup.assert_not_called()
        self.assertEqual(('control', 'command', 0), verifier.PHASE.get())

    def test_Given_DiagnosticSinkFailure_When_CommandFails_Then_TeardownAndFailurePropagationRemainMandatory(self):
        """Broken output during failure/cleanup cannot bypass process cleanup or replace its failure."""
        for event in ('command-failed', 'cleanup-returned', 'cleanup-incomplete'):
            error = subprocess.TimeoutExpired('synthetic-phase-secret', 180)
            cleanup_error = subprocess.TimeoutExpired('synthetic-phase-secret', 10) if event == 'cleanup-incomplete' else None
            with self.subTest(event=event):
                _, process = diagnostic_run(error=error, cleanup_error=cleanup_error, sink_failure=event)
                self.assertEqual([180, 10, 10], [call.kwargs['timeout'] for call in process.communicate.call_args_list])
                process.stdout.close.assert_called_once()
                process.wait.assert_called_once_with(timeout=10)

    def test_Given_CancellationDuringFailureReporting_When_Unwinding_Then_CleanupStillRuns(self):
        """Even a BaseException outside best-effort reporting cannot skip the teardown finally."""
        process = Mock(pid=123)
        process.communicate.side_effect = subprocess.TimeoutExpired('private', 180)
        with patch.object(verifier.subprocess, 'Popen', return_value=process), \
                patch.object(verifier, 'diagnostic', side_effect=[None, KeyboardInterrupt(), None]), \
                patch.object(verifier, 'cleanup_process') as cleanup:
            with self.assertRaises(KeyboardInterrupt):
                run(['private'], 180, {})
        cleanup.assert_called_once_with(process, 10)

    def test_Given_InvalidPhase_When_Requested_Then_NoCommandRuns(self):
        """Reject arbitrary diagnostic labels and out-of-range ordinals before execution."""
        for role, operation, ordinal in [('secret', 'build', 0), ('baseline', 'secret', 0), ('mutant', 'build', -1),
                                         ('mutant', 'build', 9), ('mutant', 'build', True)]:
            action = Mock()
            with self.assertRaises(verifier.MutationError):
                verifier.perform(role, operation, action, ordinal=ordinal)
            action.assert_not_called()
        self.assertEqual(('control', 'command', 0), verifier.PHASE.get())

    def test_Given_OperationWrappers_When_Invoked_Then_OriginalBudgetsArePreserved(self):
        """Build/discovery/execution retain their original per-command budgets and roles."""
        observed = []
        def supervised(*args, **kwargs):
            observed.append((verifier.PHASE.get(), args[1]))
            return 0, ''
        with tempfile.TemporaryDirectory() as directory, patch.object(verifier, 'run', side_effect=supervised), \
                patch.object(verifier, 'catalogue', return_value={}), patch.object(verifier, 'validate_report', return_value={}):
            verifier.perform('baseline', 'build', verifier.build, {})
            verifier.perform('mutant', 'discovery', verifier.discover, 'private', {}, ordinal=1)
            verifier.perform('restored', 'execution', execute, 'private', set(), Path(directory), {})
        self.assertEqual([(('baseline', 'build', 0), 180), (('mutant', 'discovery', 1), 60),
                          (('restored', 'execution', 0), 180)], observed)

    def test_Given_IncompleteEvidenceRole_When_TimeoutOccurs_Then_SourceRestoresAndNoArtifactExists(self):
        """Incomplete baseline, mutant and restored controls cannot leave stale acceptance evidence."""
        for role in ('baseline', 'mutant', 'restored'):
            with self.subTest(role=role), tempfile.TemporaryDirectory() as directory:
                incomplete_main(Path(directory), role)
            self.assertEqual(('control', 'command', 0), verifier.PHASE.get())

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

    def test_only_the_observed_native_mtp_failure_diagnostic_is_accepted(self):
        for text in (FAILURE_DIAGNOSTIC.replace("'2'", "'3'"), FAILURE_DIAGNOSTIC + ' Cleanup failed',
                     '[xUnit.net 00:00:01.20]     ' + NAME + ' [FAIL]'):
            value = report()
            value.find('.//RunInfo/Text').text = text
            with self.subTest(text=text), self.assertRaises(ValueError):
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

    def test_Given_ReviewedMutationManifests_When_TargetsAreResolved_Then_ExactInventoriesAndUniqueSubstitutionsAreRequired(self):
        """Keep issuer and administrator inventories explicit and reject drift or ambiguous source substitutions."""
        self.assertEqual(7, len(manifest(ROOT)))
        self.assertEqual(8, len(manifest(ROOT, "administrator")))
        self.assertEqual(6, len(manifest(ROOT, "governance")))
        with self.assertRaises(ValueError): manifest(ROOT, "unknown")


    def test_Given_GovernanceManifest_When_InventoryOrSourceBoundaryDrifts_Then_VerificationRejectsIt(self):
        """Keep all six targets and the sole witness-lock exception explicit without touching sources."""
        reviewed = json.loads((ROOT / 'eng/governance-mutations.json').read_text())
        reader = Path.read_text
        for change in ('missing', 'duplicate', 'outside', 'neighbor', 'extension', 'missing-anchor', 'ambiguous-anchor', 'same-replacement'):
            value = json.loads(json.dumps(reviewed))
            item = value['mutations'][0]
            if change == 'missing': value['mutations'].pop()
            elif change == 'duplicate': value['mutations'][1]['id'] = item['id']
            elif change == 'outside': item['path'] = 'src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Accounts.cs'
            elif change == 'neighbor': item['path'] = 'src/persistence/IdentityPlatform.Infrastructure/Features/Identity/Ceremonies/CeremonyStore.cs'
            elif change == 'extension': item['path'] += '.txt'
            elif change == 'missing-anchor': item['before'] = 'seeded anchor absent from source'
            elif change == 'same-replacement': item['after'] = item['before']
            target = (ROOT / item['path']).resolve()
            def read(path, *arguments, **keywords):
                """Substitute only seeded manifest/source reads and preserve every other source byte."""
                if path == ROOT / 'eng/governance-mutations.json': return json.dumps(value)
                original = reader(path, *arguments, **keywords)
                return original + item['before'] if change == 'ambiguous-anchor' and path == target else original
            with self.subTest(change=change), patch.object(Path, 'read_text', read):
                with self.assertRaises(ValueError): manifest(ROOT, 'governance')

    def test_Given_MutationBuild_When_Started_Then_SerialNodesAndExistingDeadlineAreRequired(self):
        """Prevent overlapping build nodes without widening the existing build budget."""
        environment = {'CONTROL': 'synthetic'}
        with patch.object(verifier, 'run', return_value=(0, '')) as command:
            verifier.build(environment)
        command.assert_called_once_with(['dotnet', 'build', verifier.PROJECT, '--no-restore', '--nologo', '-m:1', '-nr:false'], 180, environment)
        with patch.object(verifier, 'run', return_value=(1, '')):
            with self.assertRaises(ValueError): verifier.build(environment)


    def test_Given_AdministratorMutantDiscovery_When_MetadataDiffers_Then_IdentityCatalogueCannotAcceptIt(self):
        """Bind administrator mutant discovery to the reviewed metadata before the existing UID checks."""
        for project in ('Administrator', 'Issuer'):
            with self.subTest(project=project), patch.object(verifier, 'PROJECT', project), \
                    patch.object(verifier, 'run', return_value=(0, 'native-discovery')), \
                    patch.object(verifier, 'load_inventory', return_value={'reviewed': True}) as loader, \
                    patch.object(verifier, 'require_native_inventory') as require, \
                    patch.object(verifier, 'catalogue', return_value={'selected': True}) as catalogue:
                self.assertEqual({'selected': True}, verifier.discover('control', {}))
                if project == 'Administrator':
                    loader.assert_called_once_with(ROOT / 'eng/administrator-test-matrix.json')
                    require.assert_called_once_with('native-discovery', {'reviewed': True})
                    catalogue.reset_mock()
                    require.side_effect = ValueError('Seeded metadata mismatch')
                    with self.assertRaises(ValueError): verifier.discover('control', {})
                    catalogue.assert_not_called()
                else:
                    loader.assert_not_called()
                    require.assert_not_called()


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
                    self.assertTrue(wait_for_termination(pid))
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

    def test_only_terminal_proc_states_count_as_termination(self):
        for state in ('Z', 'X', 'R', 'S', 'D', 'T', 't', 'P', 'I', '?'):
            for name in ('python3', 'worker (with spaces)'):
                with self.subTest(state=state, name=name), patch.object(Path, 'read_text', return_value=f'123 ({name}) {state} 1 2 3'):
                    self.assertEqual(state in ('Z', 'X'), terminated(123))
        for malformed in ('', '123 no delimiter', '123 (python3)'):
            with self.subTest(malformed=malformed), patch.object(Path, 'read_text', return_value=malformed):
                self.assertFalse(terminated(123))

    def test_first_terminal_observation_is_preserved(self):
        records = ['123 (python3) R 1 2', '123 (python3) Z 1 2', '123 (python3) X 1 2']
        with patch.object(Path, 'read_text', side_effect=records) as reader, patch('time.monotonic', return_value=0), patch('time.sleep'):
            self.assertTrue(wait_for_termination(123))
            self.assertEqual(2, reader.call_count)

    def test_live_process_until_deadline_is_not_termination(self):
        with patch.object(Path, 'read_text', return_value='123 (python3) S 1 2'), patch('time.monotonic', side_effect=[0, 0, 1]), patch('time.sleep'):
            self.assertFalse(wait_for_termination(123))

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
