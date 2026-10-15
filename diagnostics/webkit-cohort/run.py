"""Separate native MTP, covered child and collector exits for one diagnostic attempt."""
import json
import hashlib
import os
from pathlib import Path
import sys
import time
from common import PACKET, SOURCE, FILTER, read, sha, write, source_hashes, environment
from evidence import observation
from cohort import native
import overlay
from outcome import job_success
from process import run as owned_run


def supervisor(repo):
    sys.path.insert(0, str(repo / 'scripts'))
    import verify_contrast_experiment
    verify_contrast_experiment.install_cancellation_handler()
    return verify_contrast_experiment


def native_once(repo, work):
    helper = supervisor(repo)
    before = json.loads(read(work / 'source-before.json'))
    assert source_hashes(repo) == before
    assert overlay.unchanged(work)
    selected = json.loads(read(work / 'selection.json'))
    manifest = json.loads(read(PACKET / 'patch-manifest.json'))
    driver = work / 'driver/.playwright/package/lib'
    assert sha(driver / 'coreBundle.js') == manifest['patched_sha256']
    assert sha(driver / 'identity-webkit-observer.cjs') == manifest['observer_sha256']
    assert not (work / 'private/native-observation.json').exists()
    assert not (work / 'private/native-observation.json.armed').exists()
    assert not (work / 'private/native-observation.json.restored').exists()
    results = work / 'private/results'; results.mkdir(mode=0o700, exist_ok=False)
    with (work / 'private/execution-started.json').open('x') as stream:
        json.dump({'source': SOURCE, 'run_id': os.environ['GITHUB_RUN_ID'], 'attempt': 1}, stream)
    cmd = ['dotnet', 'test', '--project', str(work / 'instrumented/tests/aspire/IdentityPlatform.Issuer.Tests/IdentityPlatform.Issuer.Tests.csproj'),
           '--no-build', '--no-restore', '--no-progress', '--report-trx', '--report-trx-filename', 'certificate.trx',
           '--results-directory', str(results), '--pre-enumerate-theories', 'on', '--filter', FILTER]
    native_exit, child_exit, started = None, 1, time.monotonic()
    try:
        native_exit = owned_run(cmd, environment(work), repo, work / 'private/native-process.json', 660)
    except BaseException:
        pass
    finally:
        write(work / 'public/native-exit.json', {'native_exit': native_exit,
              'seconds': round(time.monotonic() - started, 3), 'acceptance': False})
    try:
        raw = read(results / 'certificate.trx')
        report = native(raw, selected, native_exit, json.loads(read(PACKET / 'historical-cohort.json')))
        write(work / 'public/native-result.json', {**report, 'report_valid': True,
              'report_sha256': hashlib.sha256(raw).hexdigest()})
        assert read(work / 'private/native-observation.json.armed') == b'certificate-session-v1'
        assert read(work / 'private/native-observation.json.restored') == b'parent-variables-absent-v1'
        raw = read(work / 'private/native-observation.json', 128 * 1024)
        diagnostic = observation(raw)
        diagnostic['diagnostic_complete'] &= source_hashes(repo) == before
        diagnostic['diagnostic_complete'] &= overlay.unchanged(work)
        diagnostic['certificate_session_armed_once'] = True
        diagnostic['parent_variables_restored'] = True
        write(work / 'public/observation.json', json.loads(raw))
        write(work / 'public/diagnostic-result.json', diagnostic)
        if diagnostic['diagnostic_complete']: child_exit = native_exit
    except BaseException:
        write(work / 'public/validation-failure.json', {'category': 'missing-or-invalid-evidence', 'acceptance': False})
    write(work / 'public/child-exit.json', {'child_exit': child_exit, 'acceptance': False})
    return child_exit


def collect(repo, work):
    helper = supervisor(repo)
    command = ['dotnet', 'coverage', 'collect', '-s', str(repo / 'eng/coverage.xml'),
               '-o', str(work / 'private/coverage.cobertura.xml'), '-f', 'cobertura',
               'python3', str(PACKET / 'run.py'), 'native', str(repo), str(work)]
    collector_exit = None
    # Only this child and collector use the private output file; no raw console enters Actions logs.
    with (work / 'private/console.log').open('xb') as log:
        saved = [os.dup(1), os.dup(2)]
        try:
            os.dup2(log.fileno(), 1); os.dup2(log.fileno(), 2)
            collector_exit = owned_run(command, os.environ.copy(), repo, work / 'private/collector-process.json', 720)
        except BaseException:
            pass
        finally:
            for fd, backup in zip((1, 2), saved): os.dup2(backup, fd); os.close(backup)
            write(work / 'public/collector-exit.json', {'collector_exit': collector_exit, 'acceptance': False})
    return 0 if collector_exit == 0 else 1


def finish(work):
    public = work / 'public'; public.mkdir(mode=0o700, parents=True, exist_ok=True)
    def receipt(name):
        try: return json.loads(read(public / name))
        except (OSError, ValueError): return {}
    native_exit = receipt('native-exit.json').get('native_exit')
    child_exit = receipt('child-exit.json').get('child_exit')
    collector_exit = receipt('collector-exit.json').get('collector_exit')
    ok = job_success(native_exit, child_exit, collector_exit, receipt('native-result.json'),
                     receipt('diagnostic-result.json'), receipt('cleanup.json'))
    write(public / 'outcome.json', {'native_exit': native_exit, 'child_exit': child_exit,
          'collector_exit': collector_exit, 'diagnostic_job_success': ok, 'acceptance': False,
          'source': SOURCE, 'run_id': os.environ.get('GITHUB_RUN_ID'), 'diagnostic_sha': os.environ.get('GITHUB_SHA')})
    print('Focused diagnostic complete; acceptance remains false.' if ok else 'Focused diagnostic failed or incomplete; acceptance remains false.')
    return 0 if ok else 1


if __name__ == '__main__':
    try:
        mode, repo, work = sys.argv[1], Path(sys.argv[2]).resolve(), Path(sys.argv[3]).resolve()
        if mode == 'collect': code = collect(repo, work)
        elif mode == 'native': code = native_once(repo, work)
        elif mode == 'finish': code = finish(work)
        else: code = 1
    except BaseException:
        print('Diagnostic prerequisite or execution failed; private details withheld.')
        code = 1
    raise SystemExit(code)
