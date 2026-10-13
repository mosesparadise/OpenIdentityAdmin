#!/usr/bin/env python3
"""Run only A-006's gated SQL experiments; outputs never constitute suite acceptance."""
import argparse
import fcntl
import hashlib
import json
import os
from pathlib import Path
import signal
import sys
import tempfile

import verify_mutations as verifier
from administrator_inventory import unique_object

PRIVILEGE_METHOD = ('IdentityPlatform.Administrator.Tests.Features.Governance.Failures.PersistenceTests.'
                    'Given_RuntimeDatabasePrincipal_When_GovernedChangesRun_Then_RequiredDmlWorksAndSchemaWritesDeny')
WITNESS_ID = 'omitted-witness-lock'
AUTHORIZATION = 'A-006'
ARTIFACT_ROOT = 'artifacts/governance-diagnostics-a006'
SQL_IMAGE = 'mcr.microsoft.com/mssql/server@sha256:4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090'
DOCKER_SOCKET = 'unix:///var/run/docker.sock'


def require_image(environment):
    """Inspect the same managed daemon used by the child graph; never implicitly acquire an image."""
    if environment.get('DOCKER_HOST') != DOCKER_SOCKET:
        raise verifier.MutationError('Diagnostic Docker endpoint differs from the managed daemon')
    code, _ = verifier.run(['docker', '--host=' + DOCKER_SOCKET, 'image', 'inspect', SQL_IMAGE, '--format', '{{.Id}}'], 30, environment)
    if code:
        raise verifier.MutationError('Pinned SQL image is unavailable; no database experiment is permitted')


def require_passing_ordinary(prior, commit):
    """A006 requires exact-source two-case strict success, never a previous incomplete diagnostic receipt."""
    if not isinstance(prior, dict):
        raise verifier.MutationError('Invalid A006 ordinary receipt')
    expected = {'authorization': AUTHORIZATION, 'source_commit': commit, 'mode': 'ordinary', 'provider': 'SqlServer',
                'diagnostic_only': True, 'source_restored': True, 'halt_for_observed_bypass': False,
                'outcome': 'expected-outcome-observed'}
    if any(type(prior.get(key)) is not type(value) or prior[key] != value for key, value in expected.items()):
        raise verifier.MutationError('Same-source successful A006 ordinary receipt required')
    observation = prior.get('observation', {})
    if not isinstance(observation, dict) or not isinstance(observation.get('counters'), dict):
        raise verifier.MutationError('Invalid A006 ordinary observation')
    counters = observation.get('counters', {})
    counts = {name: 2 if name in ('total', 'executed', 'passed') else 0 for name in verifier.COUNTER_NAMES}
    if set(counters) != set(counts) or any(type(counters[key]) is not int or counters[key] != value for key, value in counts.items()):
        raise verifier.MutationError('A006 requires both ordinary cases passed without incomplete counters')
    if observation.get('report') != 'parsed' or any(type(observation.get(key)) is not int or observation[key] != 0
                                                  for key in ('error_nodes', 'summary_errors')):
        raise verifier.MutationError('A006 ordinary report contains errors or missing evidence')


def require_selection(selection, provider, methods):
    """Bind exactly the reviewed provider/method set, retaining native IDs and metadata."""
    names = [test[0].split('(')[0] for test in selection.values()]
    if sorted(names) != sorted(methods) or any(test[1] != provider or test[2] for test in selection.values()):
        raise verifier.MutationError('Diagnostic selection differs from approved scope')


def attempt(expression, selection, directory, environment, item, mode):
    """Project success and failure alike without converting a failed ordinary check into acceptance."""
    outcome = 'incomplete'
    result = None
    try:
        result = verifier.perform('mutant' if mode == 'mutant' else 'baseline', 'execution', verifier.execute,
            expression, {test[0] for test in selection.values()}, directory, environment,
            item=item if mode == 'mutant' else None, ordinal=2 if mode == 'mutant' else 0)
        outcome = 'expected-outcome-observed'
    except (ValueError, OSError, RuntimeError) as error:
        # No raw exception payload or temporary TRX is published.
        outcome = verifier.failure_category(error)
    observation = verifier.report_observation(directory / 'result.trx', item)
    bypass = mode == 'ordinary' and (observation.get('marker_match', False) or any(
        isinstance(row, dict) and (row.get('alter') == 'Succeeded' or row.get('probe') == 'Present')
        for row in observation.get('checkpoints', [])))
    return {'outcome': outcome, 'seconds': result['seconds'] if result else None,
            'observation': observation, 'halt_for_observed_bypass': bypass}


def write_receipt(path, receipt, *, unwinding=False):
    """A broken local sink cannot replace an active failure; without one it fails closed."""
    try:
        path.write_text(json.dumps(receipt, indent=2) + '\n')
    except BaseException:
        if not unwinding:
            raise


def experiment(root, output, mode, provider, environment, commit):
    """Build/discover once and restore a single reviewed SQL mutant in an isolated clean checkout."""
    item = next(item for item in verifier.manifest(root, 'governance') if item['id'] == WITNESS_ID)
    methods = [item['method']] + ([PRIVILEGE_METHOD] if mode == 'ordinary' else [])
    expression = f'Provider={provider}&(' + '|'.join('FullyQualifiedName=' + method for method in methods) + ')'
    verifier.perform('baseline', 'build', verifier.build, environment)
    selection = verifier.perform('baseline', 'discovery', verifier.discover, expression, environment)
    require_selection(selection, provider, methods)
    path = root / item['path']
    before = hashlib.sha256(path.read_bytes()).hexdigest()
    receipt = {'diagnostic_only': True, 'authorization': AUTHORIZATION, 'mode': mode, 'provider': provider, 'source_before': before, 'source_commit': commit}
    try:
        with tempfile.TemporaryDirectory(prefix='a006-', dir=root / 'work') as temporary:
            directory = Path(temporary)
            if mode == 'ordinary':
                receipt.update(attempt(expression, selection, directory, environment, item, mode))
            else:
                with verifier.changed_source(path, item['before'], item['after']):
                    receipt['source_mutant'] = hashlib.sha256(path.read_bytes()).hexdigest()
                    verifier.perform('mutant', 'build', verifier.build, environment, ordinal=2)
                    changed = verifier.perform('mutant', 'discovery', verifier.discover, expression, environment, ordinal=2)
                    if changed != selection:
                        raise verifier.MutationError('Diagnostic mutation changed native identities')
                    receipt.update(attempt(expression, selection, directory, environment, item, mode))
    finally:
        receipt['source_restored'] = hashlib.sha256(path.read_bytes()).hexdigest() == before
        write_receipt(output / 'diagnostic.json', receipt, unwinding=sys.exception() is not None)
    return receipt


def main():
    """Claim one local attempt, preserving every ordinary result before permitting the SQL diagnostic mutant."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('mode', choices=('ordinary', 'mutant'))
    parser.add_argument('provider', choices=('SqlServer',))
    parser.add_argument('--disposable-checkout', action='store_true', required=True)
    args = parser.parse_args()
    root = verifier.ROOT
    verifier.PROJECT = 'tests/aspire/IdentityPlatform.Administrator.Tests/IdentityPlatform.Administrator.Tests.csproj'
    environment = os.environ.copy()
    environment.update(DOTNET_ENVIRONMENT='Development', TESTINGPLATFORM_TELEMETRY_OPTOUT='1')
    for name in ('DOCKER_CONTEXT', 'DOCKER_TLS', 'DOCKER_TLS_VERIFY', 'DOCKER_CERT_PATH'):
        environment.pop(name, None)
    environment['DOCKER_HOST'] = DOCKER_SOCKET
    signal.signal(signal.SIGTERM, lambda signum, frame: (_ for _ in ()).throw(SystemExit(128 + signum)))
    private = root / 'work'
    private.mkdir(exist_ok=True)
    with (private / 'critical-mutations.lock').open('w') as lock:
        fcntl.flock(lock, fcntl.LOCK_EX | fcntl.LOCK_NB)
        verifier.require_clean_checkout(environment)
        code, commit = verifier.run(['git', 'rev-parse', 'HEAD'], 30, environment)
        if code:
            raise verifier.MutationError('Diagnostic source identity unavailable')
        run_once(root, args.mode, args.provider, environment, commit.strip())


def run_once(root, mode, provider, environment, commit):
    """Require SQL readiness and a passing ordinary comparison before the single conditional mutant."""
    if provider != 'SqlServer':
        raise verifier.MutationError('A006 permits only SQL diagnostic execution')
    require_image(environment)
    base = root / ARTIFACT_ROOT
    for prior in base.glob('ordinary-*/diagnostic.json'):
        if json.loads(prior.read_text(), object_pairs_hook=unique_object).get('halt_for_observed_bypass'):
            raise verifier.MutationError('Unmodified trust observation requires remediation decision')
    if mode == 'mutant':
        prior = json.loads((base / 'ordinary-SqlServer' / 'diagnostic.json').read_text(), object_pairs_hook=unique_object)
        require_passing_ordinary(prior, commit)
    output = base / (mode + '-' + provider)
    output.mkdir(parents=True, exist_ok=True)
    with (output / 'attempt.json').open('x') as claim:
        json.dump({'source_commit': commit, 'diagnostic_only': True}, claim)
    token = verifier.RECEIPT.set((output / 'failure-diagnostic.json', commit, environment.get('GITHUB_RUN_ID', '')))
    try:
        receipt = experiment(root, output, mode, provider, environment, commit)
        print(json.dumps(receipt, sort_keys=True), flush=True)
    finally:
        verifier.RECEIPT.reset(token)


if __name__ == '__main__':
    try:
        main()
    except Exception:
        print('A-006 diagnostic incomplete; no acceptance evidence produced.', flush=True)
        raise SystemExit(1) from None
