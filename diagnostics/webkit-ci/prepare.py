"""Prepare a hash-bound private driver and one-case discovery; never execute tests."""
import json
import os
from pathlib import Path
import shutil
import sys
from common import PACKET, SOURCE, ORIGINAL, TEST, FILTER, command, sha, write, source_hashes


def patch(original, target):
    """Apply only the ten previously reviewed insertions and verify exact reversal."""
    manifest = json.loads((PACKET / 'patch-manifest.json').read_text())
    assert sha(original / 'package/lib/coreBundle.js') == ORIGINAL == manifest['original_sha256']
    assert sha(PACKET / 'observer.cjs') == manifest['observer_sha256']
    target.mkdir(parents=True)
    shutil.copytree(original / 'package', target / 'package')
    shutil.copytree(original / 'node/linux-x64', target / 'node/linux-x64')
    text = (original / 'package/lib/coreBundle.js').read_text()
    start = text.index('// packages/playwright-core/src/server/webkit/wkPage.ts')
    end = text.index('// packages/playwright-core/src/server/webkit/wkBrowser.ts', start)
    part = text[start:end]
    for item in manifest['hooks']:
        assert part.count(item['before']) == 1
        part = part.replace(item['before'], item['after'])
    reverse = part
    for item in reversed(manifest['hooks']):
        assert reverse.count(item['after']) == 1
        reverse = reverse.replace(item['after'], item['before'])
    assert text[:start] + reverse + text[end:] == text
    patched = 'const __identityWk = require("./identity-webkit-observer.cjs");\n' + text[:start] + part + text[end:]
    (target / 'package/lib/coreBundle.js').write_text(patched)
    shutil.copyfile(PACKET / 'observer.cjs', target / 'package/lib/identity-webkit-observer.cjs')
    assert sha(target / 'package/lib/coreBundle.js') == manifest['patched_sha256']


def prepare(repo, work):
    work.mkdir(mode=0o700, exist_ok=False)
    (work / 'private').mkdir(mode=0o700)
    (work / 'public').mkdir(mode=0o700)
    sys.path.insert(0, str(repo / 'scripts'))
    from issuer_selection import issuer_catalogue, require_issuer_inventory
    from verify_contrast_experiment import execute
    hashes = source_hashes(repo)
    assert len(hashes) == 1105
    write(work / 'source-before.json', hashes)
    original = Path(os.environ['NUGET_PACKAGES']) / 'microsoft.playwright/1.63.0/.playwright'
    write(work / 'private/driver.json', {'original': str(original)})
    patch(original, work / 'driver/.playwright')
    node = str(work / 'driver/.playwright/node/linux-x64/node')
    env = os.environ.copy(); env['PATH'] = str(Path(node).parent) + os.pathsep + env['PATH']
    execute([node, str(PACKET / 'test-observer.cjs')], env, timeout=60)
    execute(['python3', str(PACKET / 'test-evidence.py')], env, timeout=60)
    execute(['python3', str(PACKET / 'test-outcome.py')], env, timeout=60)
    execute(['python3', str(PACKET / 'test-process.py')], env, timeout=60)
    execute(['python3', str(PACKET / 'test-cleanup.py')], env, timeout=60)
    execute(['python3', str(PACKET / 'test-failure.py')], env, timeout=60)
    project = repo / 'tests/aspire/IdentityPlatform.Issuer.Tests/IdentityPlatform.Issuer.Tests.csproj'
    discover = ['dotnet', 'run', '--project', str(project), '--no-build', '--no-restore', '--', '--pre-enumerate-theories', 'on', '--list-tests', 'json']
    all_tests = issuer_catalogue(execute(discover, env, timeout=60, capture=True))
    require_issuer_inventory(all_tests, json.loads((repo / 'eng/test-matrix.json').read_text()))
    selected = issuer_catalogue(execute(discover + ['--filter', FILTER], env, timeout=60, capture=True))
    expected = {uid: item for uid, item in all_tests.items() if item[0] == TEST + '(provider: "PostgreSql")'}
    assert len(all_tests) == 644 and len(expected) == 1 and selected == expected
    assert all(item[1:3] == ('PostgreSql', True) for item in selected.values())
    write(work / 'selection.json', selected)
    assert not command(['docker', 'ps', '-aq']) and not command(['docker', 'volume', 'ls', '-q'])
    write(work / 'private/docker-baseline.json', {'containers': [], 'volumes': []})
    browser = Path(os.environ['PLAYWRIGHT_BROWSERS_PATH']) / 'webkit-2359'
    browser_files = {str(path.relative_to(browser)): sha(path) for path in sorted(browser.rglob('*')) if path.is_file()}
    assert browser_files
    write(work / 'public/browser-files.json', browser_files)
    browser_manifest = json.loads((original / 'package/browsers.json').read_text())
    assert any(row['name'] == 'webkit' and row['revision'] == '2359' for row in browser_manifest['browsers'])
    write(work / 'public/preflight.json', {'source': SOURCE, 'inventory': len(all_tests), 'selected': 1,
          'sdk': command(['dotnet', '--version'], repo), 'runtimes': command(['dotnet', '--list-runtimes']).splitlines(),
          'node': command([node, '--version']), 'playwright': '1.63.0', 'webkit_revision': 2359,
          'runner_os': os.environ.get('RUNNER_OS'), 'runner_image_os': os.environ.get('ImageOS'),
          'runner_image_version': os.environ.get('ImageVersion'), 'observer_sha256': sha(PACKET / 'observer.cjs'),
          'driver_original_sha256': ORIGINAL, 'driver_patched_sha256': sha(work / 'driver/.playwright/package/lib/coreBundle.js'),
          'public_leaf_sha256': sha(Path(os.environ['IDENTITY_TEST_CERTIFICATE'])),
          'coverage_settings_sha256': sha(repo / 'eng/coverage.xml'), 'acceptance': False})


if __name__ == '__main__':
    prepare(Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve())
