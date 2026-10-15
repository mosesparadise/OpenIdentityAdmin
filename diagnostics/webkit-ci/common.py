"""Finite file handling and exact identities for one private CI diagnostic."""
import hashlib
import json
import os
from pathlib import Path
import subprocess

SOURCE = '6fcb96216eca0974041d3e8346adc4ce8458789d'
ORIGINAL = '549070af3acabb3efcc4f55bfe6210f9f7c2fcf633cf7eaa59bfe60719969171'
TEST = 'IdentityPlatform.Issuer.Tests.Features.Browser.LoginBrowserTests.Given_ExplicitBrowserTrust_When_UnrelatedCertificateIsPresented_Then_CertificateValidationRejectsIt'
FILTER = 'Provider=PostgreSql&Category=Browser&FullyQualifiedName=' + TEST
PACKET = Path(__file__).resolve().parent


def read(path, limit=2 * 1024 * 1024):
    with Path(path).open('rb') as stream:
        raw = stream.read(limit + 1)
    if len(raw) > limit: raise ValueError('Evidence exceeds fixed bound')
    return raw


def sha(path):
    digest = hashlib.sha256()
    with Path(path).open('rb') as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b''): digest.update(block)
    return digest.hexdigest()


def write(path, value):
    path = Path(path)
    path.write_text(json.dumps(value, sort_keys=True, indent=2) + '\n')
    path.chmod(0o600)


def command(args, cwd=None):
    return subprocess.check_output(args, cwd=cwd, text=True, stderr=subprocess.DEVNULL, timeout=30).strip()


def source_hashes(repo):
    if command(['git', 'rev-parse', 'HEAD'], repo) != SOURCE: raise ValueError('Source identity mismatch')
    if command(['git', 'status', '--porcelain', '--untracked-files=no'], repo): raise ValueError('Source changed')
    names = [name for name in command(['git', 'ls-files', '-z'], repo).split('\0') if name]
    return {name: sha(repo / name) for name in names}


def environment(work):
    env = os.environ.copy()
    for key in ('DEBUG', 'PWDEBUG', 'DEBUG_FILE', 'PLAYWRIGHT_NODEJS_PATH',
                'PLAYWRIGHT_DRIVER_SEARCH_PATH', 'IDENTITY_WEBKIT_PROVENANCE'):
        env.pop(key, None)
    env.update(IDENTITY_TEST_BROWSER='webkit', DOTNET_ENVIRONMENT='Development',
               PLAYWRIGHT_DRIVER_SEARCH_PATH=str(work / 'driver'),
               IDENTITY_WEBKIT_PROVENANCE=str(work / 'private/native-observation.json'),
               IDENTITY_TEST_ARTIFACTS=str(work / 'private/results'))
    return env
