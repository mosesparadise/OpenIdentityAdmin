#!/usr/bin/env python3
"""Run only A-005's bounded experiments; outputs never constitute suite acceptance."""
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

PRIVILEGE_METHOD = ('IdentityPlatform.Administrator.Tests.Features.Governance.Failures.PersistenceTests.'
                    'Given_RuntimeDatabasePrincipal_When_GovernedChangesRun_Then_RequiredDmlWorksAndSchemaWritesDeny')
WITNESS_ID = 'omitted-witness-lock'


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
        isinstance(row, dict) and (row['alter'] == 'Succeeded' or row['probe'] == 'Present')
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
    receipt = {'diagnostic_only': True, 'mode': mode, 'provider': provider, 'source_before': before, 'source_commit': commit}
    try:
        with tempfile.TemporaryDirectory(prefix='a005-', dir=root / 'work') as temporary:
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
    parser.add_argument('provider', choices=('PostgreSql', 'SqlServer'))
    parser.add_argument('--disposable-checkout', action='store_true', required=True)
    args = parser.parse_args()
    if args.mode == 'mutant' and args.provider != 'SqlServer':
        raise verifier.MutationError('A-005 permits only the SQL witness mutation experiment')
    root = verifier.ROOT
    verifier.PROJECT = 'tests/aspire/IdentityPlatform.Administrator.Tests/IdentityPlatform.Administrator.Tests.csproj'
    environment = os.environ.copy()
    environment.update(DOTNET_ENVIRONMENT='Development', TESTINGPLATFORM_TELEMETRY_OPTOUT='1')
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
    """Refuse identical retries and require retained non-bypass ordinary receipts before mutation."""
    base = root / 'artifacts/governance-diagnostics'
    for prior in base.glob('ordinary-*/diagnostic.json'):
        if json.loads(prior.read_text()).get('halt_for_observed_bypass'):
            raise verifier.MutationError('Unmodified trust observation requires remediation decision')
    if mode == 'mutant':
        for database in ('PostgreSql', 'SqlServer'):
            prior = json.loads((base / ('ordinary-' + database) / 'diagnostic.json').read_text())
            if prior.get('source_commit') != commit or 'observation' not in prior or not prior.get('source_restored'):
                raise verifier.MutationError('Both same-source ordinary diagnostic receipts are required')
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
        print('A-005 diagnostic incomplete; no acceptance evidence produced.', flush=True)
        raise SystemExit(1) from None
