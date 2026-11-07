"""Bind coverage observations to unchanged build outputs in a trusted CI cell."""
import hashlib
import json
import os
from pathlib import Path
import subprocess
import sys
import xml.etree.ElementTree as ET
from coverage_bounds import require

ROOT = Path(__file__).resolve().parents[1]


def digest(path):
    with Path(path).open('rb') as stream:
        return hashlib.file_digest(stream, 'sha256').hexdigest()


def command(*args):
    return subprocess.check_output(args, cwd=ROOT, text=True).strip()


def build_modules(root):
    """Require all copies of each covered product DLL/PDB to agree before execution."""
    required = json.loads((root / 'eng/coverage-policy.json').read_text())['requiredModules']
    copies = {module: [] for module in required}
    for directory in ('src', 'tests'):
        for path in (root / directory).rglob('bin/Debug/net10.0/IdentityPlatform.*.dll'):
            if path.stem in copies:
                copies[path.stem].append(path)
    result = {}
    for module, paths in copies.items():
        require(paths, 'Missing build output: ' + module)
        dlls, symbols = {digest(path) for path in paths}, set()
        for path in paths:
            require(path.with_suffix('.pdb').is_file(), 'Missing symbols: ' + str(path))
            symbols.add(digest(path.with_suffix('.pdb')))
        require(len(dlls) == len(symbols) == 1, 'Inconsistent build copies: ' + module)
        result[module] = {'dllSha256': dlls.pop(), 'pdbSha256': symbols.pop()}
    return result


def context():
    require(not command('git', 'status', '--porcelain', '--untracked-files=no'), 'Tracked source is dirty')
    tools = json.loads((ROOT / 'dotnet-tools.json').read_text())
    version = command('dotnet', 'coverage', '--version')
    require(version.split('+')[0] == tools['tools']['dotnet-coverage']['version'], 'Unexpected collector version')
    cache = Path(command('dotnet', 'nuget', 'locals', 'global-packages', '--list').split(': ', 1)[1])
    package = cache / 'dotnet-coverage' / version.split('+')[0] / ('dotnet-coverage.' + version.split('+')[0] + '.nupkg')
    return {'commit': command('git', 'rev-parse', 'HEAD'), 'tree': command('git', 'rev-parse', 'HEAD^{tree}'),
            'runId': os.environ['GITHUB_RUN_ID'], 'runAttempt': os.environ['GITHUB_RUN_ATTEMPT'],
            'configuration': 'Debug', 'framework': 'net10.0', 'sdk': command('dotnet', '--version'),
            'collector': version, 'collectorSettingsSha256': digest(ROOT / 'eng/coverage.xml'),
            'collectorPackageSha256': digest(package),
            'toolManifestSha256': digest(ROOT / 'dotnet-tools.json'), 'format': 'cobertura',
            'modules': build_modules(ROOT)}


def main():
    action, label = sys.argv[1:]
    require(action in ('before', 'after') and label and all(c.isalnum() or c == '-' for c in label), 'Invalid coverage cell')
    folder = ROOT / 'artifacts/coverage'
    folder.mkdir(parents=True, exist_ok=True)
    snapshot = folder / (label + '.build.json')
    current = {'schema': 1, 'cell': label, 'build': context()}
    if action == 'before':
        require(not snapshot.exists(), 'Coverage build snapshot already exists')
        require(not (folder / (label + '.cobertura.xml')).exists(), 'Stale coverage report already exists')
        require(not (folder / (label + '.identity.json')).exists(), 'Stale coverage identity already exists')
        snapshot.write_text(json.dumps(current, indent=2) + '\n')
        return
    require(json.loads(snapshot.read_text()) == current, 'Build identity changed during coverage execution')
    report = folder / (label + '.cobertura.xml')
    modules = {node.get('name').removesuffix('.dll') for node in ET.parse(report).findall('./packages/package')}
    current['reportSha256'] = digest(report)
    current['observedModules'] = sorted(modules)
    current['attestationLimit'] = 'Trusted CI build-copy binding; not runtime loaded-module attestation.'
    (folder / (label + '.identity.json')).write_text(json.dumps(current, indent=2) + '\n')


if __name__ == '__main__':
    main()
