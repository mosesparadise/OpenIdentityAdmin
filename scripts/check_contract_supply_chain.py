"""Verify reviewed toolchain and renderer provenance without claiming a complete vendor SBOM."""
import hashlib
import json
import os
import platform
from datetime import datetime, timezone
from pathlib import Path
import urllib.request
import xml.etree.ElementTree as ET
from verify_contrast_experiment import execute, install_cancellation_handler

ROOT = Path(__file__).resolve().parents[1]
PACKAGE = ROOT / 'src/frontend/IdentityPlatform.Admin.Contracts'
POLICY = ROOT / 'eng/contracts'


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def inventory_toolchain():
    review = json.loads((POLICY / 'npm-review.json').read_text())
    lock_path = PACKAGE / 'package-lock.json'
    if digest(lock_path) != review['lockSha256']:
        raise ValueError('npm dependency graph changed without a supply-chain review')
    lock = json.loads(lock_path.read_text())
    result = []
    for location, item in lock['packages'].items():
        if not location:
            continue
        name = item.get('name', location.rsplit('node_modules/', 1)[-1])
        if item.get('license') not in review['allowedExpressions'] or not item.get('integrity', '').startswith('sha512-'):
            raise ValueError('Unreviewed npm license or missing artifact integrity')
        if not item.get('resolved', '').startswith('https://registry.npmjs.org/'):
            raise ValueError('npm artifact has an unreviewed registry source')
        installed = PACKAGE / location / 'package.json'
        if installed.exists():
            actual = json.loads(installed.read_text())
            if actual.get('name') != name or actual.get('version') != item['version'] or actual.get('license') != item['license']:
                raise ValueError('Installed package identity/license differs from its reviewed lock')
        else:
            host_os = {'Linux': 'linux', 'Darwin': 'darwin', 'Windows': 'win32'}.get(platform.system())
            host_cpu = {'x86_64': 'x64', 'AMD64': 'x64', 'aarch64': 'arm64'}.get(platform.machine())
            excluded = (item.get('os') and host_os not in item['os']) or (item.get('cpu') and host_cpu not in item['cpu'])
            if not item.get('optional') or not excluded:
                raise ValueError('Required npm dependency is not installed')
        result.append({'name': name, 'version': item['version'], 'license': item['license'], 'integrity': item['integrity']})
    return result


def verify_renderers():
    versions = {node.get('Include'): node.get('Version') for node in ET.parse(ROOT / 'Directory.Packages.props').findall('.//PackageVersion')}
    cache = Path(os.environ.get('NUGET_PACKAGES', str(Path.home() / '.nuget/packages')))
    for name in ('scalar', 'swagger'):
        record = json.loads((POLICY / 'vendor' / (name + '.json')).read_text())
        package = record['nuget']
        if versions.get(package['name']) != package['version']:
            raise ValueError('Renderer version changed without embedded-component provenance review')
        archive = cache / package['name'].lower() / package['version'] / f"{package['name'].lower()}.{package['version']}.nupkg"
        if digest(archive) != package['archiveSha256']:
            raise ValueError('Renderer archive differs from the reviewed embedded asset')
        for notice in record['notices']:
            if digest(POLICY / 'vendor' / notice['path']) != notice['sha256']:
                raise ValueError('Reviewed upstream notice changed')


def require_reviewed_advisories(current, reviewed):
    if not isinstance(current, dict):
        raise ValueError('Malformed viewer advisory response')
    identities = set()
    for name, advisories in current.items():
        if not isinstance(advisories, list) or not advisories:
            raise ValueError('Malformed viewer advisory package entry')
        for advisory in advisories:
            if not isinstance(advisory, dict) or not isinstance(advisory.get('id'), int) or advisory['id'] in identities:
                raise ValueError('Missing or duplicate viewer advisory identity')
            identities.add(advisory['id'])
            if advisory not in reviewed.get(name, []):
                raise ValueError('New or changed viewer advisory requires bounded source review')


def check_viewer_advisories():
    review = json.loads((POLICY / 'viewer-review.json').read_text())
    for file, expected in review['configurationHashes'].items():
        if digest(ROOT / file) != expected:
            raise ValueError('Viewer configuration changed without revisiting advisory dispositions')
    request_file = POLICY / 'vendor/viewer-advisory-request.json'
    response_file = POLICY / 'vendor/viewer-advisory-response.json'
    if digest(request_file) != review['requestSha256'] or digest(response_file) != review['responseSha256']:
        raise ValueError('Reviewed candidate advisory evidence changed')
    request = urllib.request.Request('https://registry.npmjs.org/-/npm/v1/security/advisories/bulk',
                                     data=request_file.read_bytes(), headers={'Content-Type': 'application/json'}, method='POST')
    with urllib.request.urlopen(request, timeout=30) as response:
        current = json.load(response)
    require_reviewed_advisories(current, json.loads(response_file.read_text()))
    return {'queriedUtc': datetime.now(timezone.utc).isoformat(), 'scope': review['scope'],
            'inventoryComplete': False, 'activationBlocked': True, 'advisories': current}


def main():
    install_cancellation_handler()
    inventory = inventory_toolchain()
    verify_renderers()
    viewer_advisories = check_viewer_advisories()
    # A nonzero audit exit, acquisition error, absent report or any advisory fails this gate.
    audit = json.loads(execute(['npm', 'audit', '--prefix', str(PACKAGE), '--json'], os.environ.copy(), timeout=60, capture=True))
    if audit.get('auditReportVersion') != 2 or audit.get('vulnerabilities') != {} or audit.get('metadata', {}).get('vulnerabilities', {}).get('total') != 0:
        raise ValueError('npm advisory report is incomplete or contains vulnerabilities')
    output = ROOT / 'artifacts/contracts/supply-chain'
    output.mkdir(parents=True, exist_ok=True)
    (output / 'npm-audit.json').write_text(json.dumps(audit, indent=2) + '\n')
    (output / 'npm-inventory.json').write_text(json.dumps(inventory, indent=2) + '\n')
    (output / 'viewer-candidate-advisories.json').write_text(json.dumps(viewer_advisories, indent=2) + '\n')
    print(f'Reviewed {len(inventory)} locked npm entries and exact renderer archives; vendor inventory/release limitations remain open.')


if __name__ == '__main__':
    main()
