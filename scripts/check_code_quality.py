#!/usr/bin/env python3
"""Run syntax-aware quality gates and prevent legacy allowances growing relative to trusted Git evidence."""
import io
import json
import os
from pathlib import Path
import subprocess
import tarfile
import tempfile

ROOT = Path(__file__).resolve().parents[1]
PROJECT = ROOT / 'eng/IdentityPlatform.CodeQuality/IdentityPlatform.CodeQuality.csproj'
ALLOWANCES = 'eng/code-quality/allowances.json'


def invoke(*arguments):
    """Run the already-built pinned tool; capture only its safe metadata report."""
    return subprocess.check_output(['dotnet', 'run', '--project', str(PROJECT), '--no-build', '--', *arguments],
                                   cwd=ROOT, text=True, timeout=120)


def finding_key(entry):
    """Compare all finding fields, including member identity and fingerprint, independent of JSON formatting."""
    return json.dumps(entry['Finding'], sort_keys=True, separators=(',', ':'))


def require_no_growth(candidate, prior):
    """Allow removal of legacy debt, but never accept new or rebound allowances as baseline maintenance."""
    old = {finding_key(entry) for entry in prior}
    new = [finding_key(entry) for entry in candidate]
    if len(new) != len(set(new)) or not set(new) <= old:
        raise ValueError('Legacy quality allowance expanded or rebound; fix code or review an explicit exception')


def trusted_revision():
    """Use event-supplied base SHA in CI; resolve the local main merge-base for developer checks."""
    value = os.environ.get('IDENTITY_QUALITY_BASE')
    if not value:
        if os.environ.get('CI'):
            raise ValueError('CI must provide the trusted code-quality comparison revision')
        value = subprocess.check_output(['git', 'merge-base', 'HEAD', 'origin/main'], cwd=ROOT, text=True).strip()
    if len(value) != 40 or any(character not in '0123456789abcdef' for character in value):
        raise ValueError('Invalid code-quality comparison commit')
    subprocess.run(['git', 'cat-file', '-e', value + '^{commit}'], cwd=ROOT, check=True, capture_output=True)
    return value


def prior_allowances(revision):
    """Read trusted inventory, or derive the initial adoption allowance solely from unchanged base source."""
    tree = subprocess.check_output(['git', 'ls-tree', '--name-only', revision, '--', ALLOWANCES], cwd=ROOT, text=True)
    if tree.strip():
        content = subprocess.check_output(['git', 'show', revision + ':' + ALLOWANCES], cwd=ROOT, text=True)
        return json.loads(content)['Legacy']
    archive = subprocess.check_output(['git', 'archive', revision, 'src', 'tests', 'eng'], cwd=ROOT)
    with tempfile.TemporaryDirectory(prefix='quality-base-') as directory:
        with tarfile.open(fileobj=io.BytesIO(archive)) as bundle:
            bundle.extractall(directory, filter='data')
        return [{'Finding': finding} for finding in json.loads(invoke('--inventory', directory))]


def main():
    """Require seeded rule controls, trusted-baseline monotonicity and all current source rules."""
    print(invoke('--self-test'), end='')
    evidence = json.loads((ROOT / ALLOWANCES).read_text())
    require_no_growth(evidence['Legacy'], prior_allowances(trusted_revision()))
    print(invoke('--check', str(ROOT)), end='')


if __name__ == '__main__':
    main()
