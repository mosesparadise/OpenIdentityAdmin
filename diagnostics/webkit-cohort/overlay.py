"""Create and restore an owned instrumented copy; never edit the pinned source checkout."""
import json
import shutil
from common import PACKET, read, sha, write, source_hashes

TEST_PATH = 'tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/LoginBrowserTests.cs'
HELPER_PATH = 'tests/aspire/IdentityPlatform.Issuer.Tests/Features/Browser/CertificateDiagnostic.cs'
OLD = 'await using var session = await BrowserSession.CreateAsync();'
NEW = 'await using var session = await CertificateDiagnostic.CreateAsync(() => BrowserSession.CreateAsync());'
METHOD = 'public async Task Given_ExplicitBrowserTrust_When_UnrelatedCertificateIsPresented_Then_CertificateValidationRejectsIt(string provider)'


def transform(text):
    assert text.count(METHOD) == 1
    start = text.index(METHOD)
    end = text.index('\n    [Theory]', start)
    part = text[start:end]
    assert part.count(OLD) == 1 and NEW not in text
    return text[:start] + part.replace(OLD, NEW) + text[end:]


def copy_source(repo, work):
    original = source_hashes(repo)
    target = work / 'instrumented'; target.mkdir(mode=0o700)
    for name in original:
        dest = target / name; dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copyfile(repo / name, dest)
    assert not (target / HELPER_PATH).exists()
    return target


def instrument(repo, work):
    original = source_hashes(repo)
    target = work / 'instrumented'
    assert all(sha(target / name) == digest for name, digest in original.items())
    assert not (target / HELPER_PATH).exists()
    path = target / TEST_PATH
    path.write_text(transform(path.read_text()))
    shutil.copyfile(PACKET / 'CertificateDiagnostic.cs', target / HELPER_PATH)
    expected = {name: sha(target / name) for name in [*original, HELPER_PATH]}
    assert {name for name in original if original[name] != expected[name]} == {TEST_PATH}
    write(work / 'instrumented-hashes.json', expected)
    write(work / 'public/instrumentation.json', {
        'acceptance': False, 'original_files': len(original), 'instrumented_files': len(expected),
        'original_test_sha256': original[TEST_PATH], 'instrumented_test_sha256': expected[TEST_PATH],
        'helper_sha256': expected[HELPER_PATH],
        'original_manifest_sha256': sha(work / 'source-before.json'),
        'instrumented_manifest_sha256': sha(work / 'instrumented-hashes.json'),
        'scope': 'one certificate factory expression and one disposable helper; rebuilt private test copy'})
    return target


def create(repo, work):
    copy_source(repo, work)
    return instrument(repo, work)


def unchanged(work):
    expected = json.loads(read(work / 'instrumented-hashes.json'))
    target = work / 'instrumented'
    source_files = {str(path.relative_to(target)) for path in target.rglob('*.cs')
                    if not {'bin', 'obj'}.intersection(path.relative_to(target).parts)}
    return (source_files == {name for name in expected if name.endswith('.cs')}
            and all(sha(target / name) == digest for name, digest in expected.items()))


def restore(repo, work):
    target = work / 'instrumented'
    def attempt(action):
        try: return action()
        except BaseException: return False
    before = attempt(lambda: unchanged(work))
    copied = attempt(lambda: bool(shutil.copyfile(repo / TEST_PATH, target / TEST_PATH)))
    def remove():
        (target / HELPER_PATH).unlink(missing_ok=True)
        return not (target / HELPER_PATH).exists()
    removed = attempt(remove)
    def verify():
        expected = json.loads(read(work / 'source-before.json'))
        return (all(sha(target / name) == digest for name, digest in expected.items())
                and source_hashes(repo) == expected)
    restored = attempt(verify)
    return bool(before and copied and removed and restored)
