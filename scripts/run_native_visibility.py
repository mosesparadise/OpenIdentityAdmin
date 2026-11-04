#!/usr/bin/env python3
"""Run only the native ApplicationLifecycle lane on an owned X11 desktop."""
import argparse
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import time

ROOT = Path(__file__).resolve().parents[1]
DRIVER = ROOT / 'tests/aspire/IdentityPlatform.Administrator.Tests/bin/Debug/net10.0/.playwright'
BUNDLE = Path('package/lib/coreBundle.js')
VERSION = '1.63.0'
ORIGINAL_SHA = '549070af3acabb3efcc4f55bfe6210f9f7c2fcf633cf7eaa59bfe60719969171'
NATIVE_SHA = 'dc9fb491407327fc249fa9287fc868d5fe79d64e45db306b00194c3cd28b0458'
OVERRIDE = b'this._pageProxySession.send("Emulation.setActiveAndFocused", { active: true })'
NATIVE = b'this._pageProxySession.send("Emulation.setActiveAndFocused", {})'


def native_bundle(content: bytes, version: str) -> bytes:
    """Unset the pinned WebKit automation override; never force visible/hidden."""
    if version != VERSION or hashlib.sha256(content).hexdigest() != ORIGINAL_SHA:
        raise ValueError('Native visibility requires the reviewed pinned driver')
    if content.count(OVERRIDE) != 1:
        raise ValueError('Native visibility override count changed')
    result = content.replace(OVERRIDE, NATIVE)
    if hashlib.sha256(result).hexdigest() != NATIVE_SHA:
        raise ValueError('Native visibility driver result changed')
    return result


def prepare_driver(source: Path, destination: Path) -> None:
    """Copy before modifying; retain a byte-for-byte original bundle witness."""
    original = (source / BUNDLE).read_bytes()
    version = json.loads((source / 'package/package.json').read_text())['version']
    replacement = native_bundle(original, version)
    shutil.copytree(source, destination / '.playwright')
    (destination / '.playwright' / BUNDLE).write_bytes(replacement)
    if (source / BUNDLE).read_bytes() != original:
        raise ValueError('Original driver changed')
    print(f'Native visibility driver {VERSION}: original {ORIGINAL_SHA}; copy {NATIVE_SHA}', flush=True)


def run(provider: str, browser: str, desktop: bool) -> None:
    """Select fixed coverage/inventory obligations; an inner process owns the WM."""
    if not desktop:
        subprocess.run(['xvfb-run', '--auto-servernum', 'python3', __file__, provider, browser, '--desktop'], check=True)
        return
    label = f'administrator-{provider}-{browser}-applicationlifecycle'
    command = ['dotnet', 'coverage', 'collect', '-s', 'eng/coverage.xml', '-o',
               f'artifacts/coverage/{label}.cobertura.xml', '-f', 'cobertura', 'python3',
               'scripts/verify.py', provider, '--partition', 'administrator', '--browser', browser,
               '--browser-story', 'ApplicationLifecycle']
    with tempfile.TemporaryDirectory(prefix='identity-native-visibility-') as directory:
        environment = dict(os.environ)
        environment.pop('PLAYWRIGHT_DRIVER_SEARCH_PATH', None)
        if browser == 'webkit':
            prepare_driver(DRIVER, Path(directory))
            environment['PLAYWRIGHT_DRIVER_SEARCH_PATH'] = directory
        manager = subprocess.Popen(['openbox', '--sm-disable'], stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        try:
            # The native property appears only after the WM claims this private display.
            deadline = time.monotonic() + 3
            while True:
                result = subprocess.run(['xprop', '-root', '_NET_SUPPORTING_WM_CHECK'], capture_output=True, text=True, timeout=1)
                if result.returncode == 0 and 'window id # 0x' in result.stdout:
                    break
                if manager.poll() is not None or time.monotonic() >= deadline:
                    raise RuntimeError('Native visibility desktop was not acquired')
                time.sleep(.05)
            subprocess.run(command, cwd=ROOT, env=environment, check=True)
        finally:
            manager.terminate()
            try:
                manager.wait(timeout=3)
            except subprocess.TimeoutExpired:
                manager.kill()
                manager.wait(timeout=3)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('provider', choices=['PostgreSql', 'SqlServer'])
    parser.add_argument('browser', choices=['chromium', 'webkit'])
    parser.add_argument('--desktop', action='store_true')
    args = parser.parse_args()
    run(args.provider, args.browser, args.desktop)
