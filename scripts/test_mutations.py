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


class GovernanceDiagnosticTests(unittest.TestCase):
    """Prove the focused diagnostic runner cannot silently grow scope or bypass its stop gates."""

    def ordinary_receipt(self):
        """Independently seed a complete two-case SQL ordinary receipt for fail-closed prerequisite controls."""
        counts = {name: 0 for name in ('total', 'executed', 'passed', 'failed', 'error', 'timeout', 'aborted', 'inconclusive',
                  'passedButRunAborted', 'notRunnable', 'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending')}
        counts.update(total=2, executed=2, passed=2)
        return {'authorization': 'A-006', 'source_commit': 'f' * 40, 'mode': 'ordinary', 'provider': 'SqlServer',
                'diagnostic_only': True, 'source_restored': True, 'halt_for_observed_bypass': False,
                'outcome': 'expected-outcome-observed',
                'observation': {'report': 'parsed', 'counters': counts, 'error_nodes': 0, 'summary_errors': 0}}

    def test_Given_MissingImageOrWrongDaemon_When_ReadinessIsChecked_Then_NoImplicitPullOccurs(self):
        """Only read-only image inspection is allowed; a missing image or wrong endpoint blocks execution."""
        import diagnostic_governance as diagnostic
        with patch.object(verifier, 'run', return_value=(1, 'private')) as run:
            with self.assertRaises(verifier.MutationError):
                diagnostic.require_image({'DOCKER_HOST': 'unix:///var/run/docker.sock'})
            self.assertEqual(['docker', '--host=unix:///var/run/docker.sock', 'image', 'inspect'], run.call_args.args[0][:4])
            self.assertEqual(30, run.call_args.args[1])
            self.assertNotIn('pull', run.call_args.args[0])
            run.reset_mock()
            with self.assertRaises(verifier.MutationError):
                diagnostic.require_image({'DOCKER_HOST': 'private'})
            run.assert_not_called()

    def test_Given_IncompleteOrForeignReceipt_When_MutantIsRequested_Then_OrdinarySuccessCannotBeInferred(self):
        """Every required field/counter must match exact A006 same-source two-case success with native JSON types."""
        import diagnostic_governance as diagnostic
        diagnostic.require_passing_ordinary(self.ordinary_receipt(), 'f' * 40)
        for key in self.ordinary_receipt():
            changed = self.ordinary_receipt()
            del changed[key]
            with self.subTest(missing=key), self.assertRaises(verifier.MutationError):
                diagnostic.require_passing_ordinary(changed, 'f' * 40)
        for key in self.ordinary_receipt()['observation']['counters']:
            for value in (None, True, '0', -1, 3):
                changed = self.ordinary_receipt()
                changed['observation']['counters'][key] = value
                with self.subTest(counter=key, value=value), self.assertRaises(verifier.MutationError):
                    diagnostic.require_passing_ordinary(changed, 'f' * 40)
        for key, value in [('authorization', 'A-005'), ('source_commit', 'e' * 40), ('outcome', 'error'),
                           ('provider', 'PostgreSql'), ('source_restored', 1), ('halt_for_observed_bypass', True),
                           ('observation', None), ('diagnostic_only', 1)]:
            changed = self.ordinary_receipt()
            changed[key] = value
            with self.subTest(field=key), self.assertRaises(verifier.MutationError):
                diagnostic.require_passing_ordinary(changed, 'f' * 40)

    def test_Given_UnavailablePrerequisite_When_RunStarts_Then_NoAttemptOrExperimentBegins(self):
        """Readiness failure stops before file claims, build, discovery or database execution."""
        import diagnostic_governance as diagnostic
        with tempfile.TemporaryDirectory() as directory, patch.object(diagnostic, 'require_image', side_effect=verifier.MutationError('fixed')), \
                patch.object(diagnostic, 'experiment') as experiment:
            with self.assertRaises(verifier.MutationError):
                diagnostic.run_once(Path(directory), 'ordinary', 'SqlServer', {}, 'f' * 40)
            self.assertEqual([], list(Path(directory).iterdir()))
            experiment.assert_not_called()

    def test_Given_ExactOrdinarySuccess_When_ConditionalMutantRuns_Then_OnlyOneClaimIsPermitted(self):
        """Valid SQL evidence permits exactly one diagnostic invocation; duplicate-key evidence is rejected."""
        import diagnostic_governance as diagnostic
        with tempfile.TemporaryDirectory() as directory, patch.object(diagnostic, 'require_image'), \
                patch.object(diagnostic, 'experiment', return_value={'diagnostic_only': True}) as experiment, redirect_stdout(io.StringIO()):
            root = Path(directory)
            base = root / diagnostic.ARTIFACT_ROOT / 'ordinary-SqlServer'
            base.mkdir(parents=True)
            receipt = base / 'diagnostic.json'
            receipt.write_text(json.dumps(self.ordinary_receipt()))
            diagnostic.run_once(root, 'mutant', 'SqlServer', {}, 'f' * 40)
            experiment.assert_called_once()
            with self.assertRaises(FileExistsError):
                diagnostic.run_once(root, 'mutant', 'SqlServer', {}, 'f' * 40)
            receipt.write_text('{"source_restored": false, "source_restored": true}')
            with self.assertRaises(ValueError):
                diagnostic.run_once(root, 'mutant', 'SqlServer', {}, 'f' * 40)
            self.assertEqual(1, experiment.call_count)

    def test_Given_SelectionDrift_When_ExperimentIsPrepared_Then_ExactProviderAndMethodsAreRequired(self):
        """Selected methods cannot be duplicated, replaced, omitted or moved to another provider."""
        import diagnostic_governance as diagnostic
        selected = {'one': ('witness(provider)', 'SqlServer', False), 'two': ('privilege(provider)', 'SqlServer', False)}
        diagnostic.require_selection(selected, 'SqlServer', ['witness', 'privilege'])
        for changed in ({'one': selected['one']}, {**selected, 'extra': ('other(provider)', 'SqlServer', False)},
                        {'one': ('witness(provider)', 'PostgreSql', False), 'two': selected['two']}):
            with self.assertRaises(verifier.MutationError):
                diagnostic.require_selection(changed, 'SqlServer', ['witness', 'privilege'])

    def test_Given_FailedMutationAndBrokenSink_When_Unwinding_Then_SourceAndPrimaryFailureArePreserved(self):
        """Exercise actual changed_source/finally with cancellation and failure while receipt writes also fail."""
        import diagnostic_governance as diagnostic
        for primary in (RuntimeError('private'), KeyboardInterrupt()):
            for secondary in (OSError('private'), KeyboardInterrupt()):
                with tempfile.TemporaryDirectory() as directory:
                    root = Path(directory)
                    (root / 'work').mkdir()
                    source = root / 'target.cs'
                    source.write_text('original')
                    item = {'id': diagnostic.WITNESS_ID, 'path': 'target.cs', 'before': 'original', 'after': 'mutant', 'method': 'witness'}
                    with patch.object(verifier, 'manifest', return_value=[item]), patch.object(verifier, 'build'), \
                            patch.object(verifier, 'discover', return_value={'one': ('witness(provider)', 'SqlServer', False)}), \
                            patch.object(diagnostic, 'attempt', side_effect=primary), patch.object(Path, 'write_text', side_effect=secondary) as writer:
                        # Keep real source writes/restoration while breaking only diagnostic output.
                        with self.assertRaises(type(primary)) as caught:
                            diagnostic.experiment(root, root, 'mutant', 'SqlServer', {}, 'f' * 40)
                    self.assertIs(primary, caught.exception)
                    self.assertEqual('original', source.read_text())
                    self.assertFalse((root / 'evidence.json').exists())

    def test_Given_ClaimedAttemptOrObservedBypass_When_NextRunStarts_Then_NoExperimentExecutes(self):
        """A previous unmodified bypass or duplicate local attempt is a stop, not a retry opportunity."""
        import diagnostic_governance as diagnostic
        with tempfile.TemporaryDirectory() as directory:
            root = Path(directory)
            base = root / diagnostic.ARTIFACT_ROOT / 'ordinary-SqlServer'
            base.mkdir(parents=True)
            (base / 'attempt.json').write_text('{}')
            with patch.object(diagnostic, 'experiment') as experiment, patch.object(diagnostic, 'require_image'):
                with self.assertRaises(FileExistsError):
                    diagnostic.run_once(root, 'ordinary', 'SqlServer', {}, 'f' * 40)
                (base / 'diagnostic.json').write_text(json.dumps({'halt_for_observed_bypass': True}))
                with self.assertRaises(verifier.MutationError):
                    diagnostic.run_once(root, 'mutant', 'SqlServer', {}, 'f' * 40)
                experiment.assert_not_called()

    def test_Given_UnmodifiedDdlSuccess_When_OrdinaryValidationFails_Then_DiagnosticRequiresHalt(self):
        """Raw output is discarded and an actual classified ALTER success cannot proceed to the mutant."""
        import diagnostic_governance as diagnostic
        with tempfile.TemporaryDirectory() as directory:
            root = report()
            ET.SubElement(root, 'StdOut').text = 'governance-setup-stage HostBuild\ngovernance-privilege Succeeded Present none'
            ET.ElementTree(root).write(Path(directory) / 'result.trx')
            with patch.object(verifier, 'execute', side_effect=verifier.MutationError('fixed')):
                result = diagnostic.attempt('private', {}, Path(directory), {}, {'marker': 'other', 'method': METHOD}, 'ordinary')
            self.assertTrue(result['halt_for_observed_bypass'])
            self.assertEqual('error', result['outcome'])

    def test_Given_SetupAndDenialObservations_When_OrdinaryRunFinishes_Then_MixedCheckpointsRemainDiagnostic(self):
        """Setup dictionaries cannot cause a post-execution classifier error or be mistaken for privilege bypasses."""
        import diagnostic_governance as diagnostic
        with tempfile.TemporaryDirectory() as directory:
            root = report(failed=False)
            ET.SubElement(root, 'StdOut').text = 'governance-setup-stage LeaseReady\ngovernance-privilege Denied Absent 229'
            ET.ElementTree(root).write(Path(directory) / 'result.trx')
            with patch.object(verifier, 'execute', return_value={'seconds': 1}):
                result = diagnostic.attempt('private', {}, Path(directory), {}, {'marker': MARKER, 'method': METHOD}, 'ordinary')
            self.assertFalse(result['halt_for_observed_bypass'])
            self.assertEqual('expected-outcome-observed', result['outcome'])
            self.assertEqual([{'setup': 'LeaseReady'}, {'alter': 'Denied', 'probe': 'Absent', 'provider_code': 229}],
                             result['observation']['checkpoints'])

    def test_Given_EarlyPrivilegeBypass_When_SetupTailOverflows_Then_HaltAndPrivilegeEvidenceSurvive(self):
        """Neither later setup observations nor a later denial may evict an observed bypass."""
        import diagnostic_governance as diagnostic
        with tempfile.TemporaryDirectory() as directory:
            root = report()
            ET.SubElement(root, 'StdOut').text = '\n'.join(
                ['governance-privilege Succeeded Present none'] + ['governance-setup-stage HostBuild'] * 100 +
                ['governance-privilege Denied Absent 229', 'governance-setup-stage LeaseReady'])
            ET.ElementTree(root).write(Path(directory) / 'result.trx')
            with patch.object(verifier, 'execute', side_effect=verifier.MutationError('fixed')):
                result = diagnostic.attempt('private', {}, Path(directory), {}, {'marker': MARKER, 'method': METHOD}, 'ordinary')
            self.assertTrue(result['halt_for_observed_bypass'])
            checkpoints = result['observation']['checkpoints']
            self.assertEqual(32, len(checkpoints))
            self.assertEqual({'alter': 'Succeeded', 'probe': 'Present', 'provider_code': None}, checkpoints[0])
            self.assertEqual({'alter': 'Denied', 'probe': 'Absent', 'provider_code': 229}, checkpoints[-2])
            self.assertEqual({'setup': 'LeaseReady'}, checkpoints[-1])


class FailureReceiptTests(unittest.TestCase):
    """Seed hostile and incomplete reports without granting mutation acceptance."""

    def test_Given_KnownHarnessMessage_When_Classified_Then_OnlyExactCodeOwnedFailuresAreRecognized(self):
        """Appending secret text cannot make it part of a retained category; unknown messages remain unknown."""
        for message, category in [('Bootstrap harness startup failed.', 'harness-startup-failed'),
                                  ('Bootstrap database setup failed.', 'harness-database-failed'),
                                  ('Bootstrap host cleanup failed.', 'harness-cleanup-failed')]:
            root = report()
            root.find('.//Message').text = message
            self.assertIn(category, verifier.report_categories(root))
            root.find('.//Message').text = message + ' synthetic-secret'
            self.assertNotIn(category, verifier.report_categories(root))
            self.assertNotIn('synthetic-secret', json.dumps(verifier.report_categories(root)))

    def test_Given_SetupCheckpointCanaries_When_Projected_Then_UnknownLabelsAndPayloadsAreDiscarded(self):
        """Native enum values are finite; resource names, token fields and appended text never become observations."""
        root = report()
        ET.SubElement(root, 'StdOut').text = '\n'.join(('governance-setup-stage HostBuild',
            'governance-setup-stage synthetic-secret', 'governance-setup-stage HostBuild synthetic-secret',
            'governance-setup-stage LeaseCreate', 'governance-setup-stage -1'))
        self.assertEqual([{'setup': 'HostBuild'}, {'setup': 'LeaseCreate'}], verifier.report_checkpoints(root))

    def test_Given_RepeatedOrInvalidCheckpoints_When_Projected_Then_OutputStaysClosedAndBounded(self):
        """Only known stages and six-digit provider codes can appear in the final 32 observations."""
        root = report()
        ET.SubElement(root, 'StdOut').text = '\n'.join(['governance-witness-stage Oracle'] * 100 +
            ['governance-privilege Denied Absent 9999999', 'governance-privilege Denied Absent private',
             'governance-witness-stage private', 'governance-privilege Unknown Unknown none'])
        checkpoints = verifier.report_checkpoints(root)
        self.assertEqual(32, len(checkpoints))
        self.assertEqual(['Oracle'] * 31, checkpoints[:-1])
        self.assertEqual({'alter': 'Unknown', 'probe': 'Unknown', 'provider_code': None}, checkpoints[-1])

    def test_Given_SecretReport_When_Projected_Then_OnlyBoundedCategoriesRemain(self):
        """Messages, stacks, names, arbitrary attributes and output must not escape the allowlist."""
        secret = 'synthetic-receipt-secret'
        root = report()
        root.find('.//Message').text = 'Xunit.Sdk.EqualException ' + secret
        root.find('.//StackTrace').text = secret
        root.find('.//UnitTestResult').set('testName', secret)
        root.find('.//Text').text = secret
        root.find('.//Counters').set('total', '9' * 100)
        root.find('.//Counters').set(secret, secret)
        ET.SubElement(root, 'StdOut').text = '\n'.join((secret, 'governance-witness-stage GateArrived',
            'governance-witness-stage ' + secret, 'governance-privilege ProviderError Absent -2',
            'governance-privilege denied absent ' + secret))
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'result.trx'
            ET.ElementTree(root).write(path)
            result = verifier.report_observation(path, {'marker': MARKER, 'method': METHOD})
        self.assertNotIn(secret, json.dumps(result))
        self.assertIsNone(result['counters']['total'])
        self.assertEqual(['EqualException'], result['categories'])
        self.assertFalse(result['marker_match'])
        self.assertFalse(result['stack_match'])
        self.assertEqual(['GateArrived', {'alter': 'ProviderError', 'probe': 'Absent', 'provider_code': -2}], result['checkpoints'])

    def test_Given_AbsentMalformedOrOversizedReport_When_Observed_Then_NoSuccessIsInvented(self):
        """Unavailable and rejected reports preserve an explicit unknown instead of parsing unbounded input."""
        cases = [(None, 'missing'), (b'<broken>', 'unreadable'),
                 (b'x' * (verifier.REPORT_LIMIT_BYTES + 1), 'rejected'), (b'<!DOCTYPE private><TestRun/>', 'rejected')]
        for contents, category in cases:
            with self.subTest(category=category), tempfile.TemporaryDirectory() as directory:
                path = Path(directory) / 'result.trx'
                if contents is not None:
                    path.write_bytes(contents)
                self.assertEqual({'report': category}, verifier.report_observation(path, None))

    def test_Given_ExpectedMarker_When_ReceiptMatches_Then_StrictValidationStillOwnsAcceptance(self):
        """A matching marker cannot mask a fixture error, regardless of observational booleans."""
        root = report()
        ET.SubElement(root.find('.//ResultSummary'), 'ErrorInfo')
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'result.trx'
            ET.ElementTree(root).write(path)
            result = verifier.report_observation(path, {'marker': MARKER, 'method': METHOD})
            self.assertTrue(result['marker_match'] and result['stack_match'])
            self.assertEqual(2, result['error_nodes'])
            with self.assertRaises(verifier.MutationError):
                validate_report(path, {NAME}, 2, marker=MARKER, method=METHOD)

    def test_Given_PrimaryFailureAndBrokenReceipt_When_ExecuteUnwinds_Then_OriginalFailureAndSourceArePreserved(self):
        """Receipt errors and second cancellation cannot hide process failure or skip source restoration."""
        for primary in (RuntimeError('private'), KeyboardInterrupt(), SystemExit(143)):
            for sink_error in (OSError('private'), KeyboardInterrupt(), None):
                with self.subTest(primary=type(primary), sink=type(sink_error)), tempfile.TemporaryDirectory() as directory:
                    base = Path(directory)
                    source = base / 'target.cs'
                    source.write_text('original')
                    receipt = Mock()
                    receipt.write_text.side_effect = sink_error
                    token = verifier.RECEIPT.set((receipt, 'f' * 40, '1234'))
                    try:
                        with patch.object(verifier, 'run', side_effect=primary), self.assertRaises(type(primary)) as caught:
                            with changed_source(source, 'original', 'mutant'):
                                execute('private', {NAME}, base, {}, item={'marker': MARKER, 'method': METHOD})
                        self.assertIs(primary, caught.exception)
                        self.assertEqual('original', source.read_text())
                        self.assertFalse((base / 'evidence.json').exists())
                        self.assertEqual('missing', json.loads(receipt.write_text.call_args.args[0])['report'])
                    finally:
                        verifier.RECEIPT.reset(token)

    def test_Given_UnexpectedAssertion_When_ExecuteFails_Then_ReceiptSurvivesPrivateReportCleanup(self):
        """Report receipt remains outside temporary results; arbitrary source/run identities are suppressed."""
        with tempfile.TemporaryDirectory() as directory:
            receipt = Path(directory) / 'failure-diagnostic.json'
            token = verifier.RECEIPT.set((receipt, 'secret-source', 'secret-run'))
            try:
                with tempfile.TemporaryDirectory(dir=directory) as private:
                    def failed_run(*args, **kwargs):
                        root = report()
                        root.find('.//Message').text = 'UnknownFailure secret-message'
                        ET.ElementTree(root).write(Path(private) / 'result.trx')
                        return 2, 'secret-output'
                    with patch.object(verifier, 'run', side_effect=failed_run), self.assertRaises(verifier.MutationError):
                        verifier.perform('mutant', 'execution', execute, 'private', {NAME}, Path(private), {},
                                         item={'marker': MARKER, 'method': METHOD}, ordinal=2)
                result = json.loads(receipt.read_text())
                self.assertNotIn('secret', receipt.read_text())
                self.assertEqual(('mutant', 'execution', 2), tuple(result[k] for k in ('role', 'operation', 'ordinal')))
                self.assertTrue(result['diagnostic_only'])
                self.assertEqual(['unknown'], result['categories'])
                self.assertIsNone(result['source_commit'])
                self.assertIsNone(result['run_id'])
            finally:
                verifier.RECEIPT.reset(token)


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
    suite = unittest.defaultTestLoader.loadTestsFromModule(sys.modules[__name__])
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    sys.exit(0 if clean_result(result) else 1)
