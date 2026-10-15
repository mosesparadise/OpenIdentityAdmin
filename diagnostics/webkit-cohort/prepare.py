"""Prepare a hash-bound private driver and 30-case cohort discovery; never execute tests."""
import json
import os
from pathlib import Path
import shutil
import sys
from common import PACKET, SOURCE, ORIGINAL, FILTER, command, sha, write, source_hashes
import overlay


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
    expected = {uid: item for uid, item in all_tests.items() if item[1:3] == ('PostgreSql', True)}
    historical = json.loads((PACKET / 'historical-cohort.json').read_text())
    assert len(all_tests) == 644 and len(expected) == 30 and selected == expected
    assert {item[0] for item in selected.values()} == {row['name'] for row in historical['native_order']}
    assert all(item[1:3] == ('PostgreSql', True) for item in selected.values())
    target = overlay.copy_source(repo, work)
    execute(['dotnet', 'restore', str(target / 'IdentityPlatform.slnx'), '--locked-mode'], env, timeout=180)
    execute(['dotnet', 'build', str(target / 'IdentityPlatform.slnx'), '--no-restore', '--nologo'], env, timeout=180)
    copied = list(discover); copied[3] = str(target / project.relative_to(repo))
    copied_all = issuer_catalogue(execute(copied, env, timeout=60, capture=True))
    copied_selected = issuer_catalogue(execute(copied + ['--filter', FILTER], env, timeout=60, capture=True))
    assert len(copied_all) == 644 and sorted(copied_all.values()) == sorted(all_tests.values())
    assert len(copied_selected) == 30 and sorted(copied_selected.values()) == sorted(selected.values())
    overlay.instrument(repo, work)
    execute(['dotnet', 'build', copied[3], '--no-restore', '--nologo'], env, timeout=180)
    instrumented_all = issuer_catalogue(execute(copied, env, timeout=60, capture=True))
    instrumented_selected = issuer_catalogue(execute(copied + ['--filter', FILTER], env, timeout=60, capture=True))
    assert instrumented_all == copied_all and instrumented_selected == copied_selected
    write(work / 'selection.json', instrumented_selected)
    write(work / 'public/discovery.json', {'acceptance': False, 'original_inventory': len(all_tests),
          'private_inventory': len(copied_all), 'instrumented_inventory': len(instrumented_all),
          'portable_names_and_metadata_equal': True, 'same_path_all_uids_equal': True,
          'same_path_selected_uids_equal': True, 'cross_path_all_uids_equal': all_tests == copied_all,
          'selected': len(instrumented_selected), 'historical_cohort_names_equal': True})
    assert overlay.unchanged(work) and source_hashes(repo) == hashes
    execute(['dotnet', 'run', '--project', str(PACKET / 'factory-controls/controls.csproj')], env, timeout=120)
    execute(['python3', str(PACKET / 'test-cohort.py')], env, timeout=60)
    execute(['python3', str(PACKET / 'test-overlay.py')], env, timeout=60)
    assert not command(['docker', 'ps', '-aq']) and not command(['docker', 'volume', 'ls', '-q'])
    write(work / 'private/docker-baseline.json', {'containers': [], 'volumes': []})
    browser = Path(os.environ['PLAYWRIGHT_BROWSERS_PATH']) / 'webkit-2359'
    browser_files = {str(path.relative_to(browser)): sha(path) for path in sorted(browser.rglob('*')) if path.is_file()}
    assert browser_files
    write(work / 'public/browser-files.json', browser_files)
    browser_manifest = json.loads((original / 'package/browsers.json').read_text())
    assert any(row['name'] == 'webkit' and row['revision'] == '2359' for row in browser_manifest['browsers'])
    write(work / 'public/preflight.json', {'source': SOURCE, 'inventory': len(all_tests), 'selected': 30,
          'sdk': command(['dotnet', '--version'], repo), 'runtimes': command(['dotnet', '--list-runtimes']).splitlines(),
          'node': command([node, '--version']), 'playwright': '1.63.0', 'webkit_revision': 2359,
          'runner_os': os.environ.get('RUNNER_OS'), 'runner_image_os': os.environ.get('ImageOS'),
          'runner_image_version': os.environ.get('ImageVersion'), 'observer_sha256': sha(PACKET / 'observer.cjs'),
          'driver_original_sha256': ORIGINAL, 'driver_patched_sha256': sha(work / 'driver/.playwright/package/lib/coreBundle.js'),
          'public_leaf_sha256': sha(Path(os.environ['IDENTITY_TEST_CERTIFICATE'])),
          'coverage_settings_sha256': sha(repo / 'eng/coverage.xml'), 'acceptance': False})


if __name__ == '__main__':
    prepare(Path(sys.argv[1]).resolve(), Path(sys.argv[2]).resolve())
