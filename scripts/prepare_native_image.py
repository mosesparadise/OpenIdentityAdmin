#!/usr/bin/env python3
"""Acquire the AppHost's pinned database image before a native test startup clock."""
import argparse
import json
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SETUPS = {provider: ROOT / f'src/aspire/IdentityPlatform.Database.{provider}.Hosting/Features/Database/{provider}DatabaseHostingProvider.cs'
          for provider in ('PostgreSql', 'SqlServer')}
PROVIDERS = {
    'PostgreSql': ('Postgres', 'docker.io/library/postgres'),
    'SqlServer': ('SqlServer', 'mcr.microsoft.com/mssql/server'),
}
PULL_SECONDS = 300
INSPECT_SECONDS = 15


def image_reference(provider, source):
    """Read one exact native image pin; reject missing, ambiguous or changed setup syntax."""
    method, repository = PROVIDERS[provider]
    if re.search(r'\.WithImage(?:Registry)?\s*\(', source):
        raise ValueError('Native database image repository override requires review')
    pattern = (rf'\bbuilder\.Add{method}\(DatabaseName\)\s*'
               r'\.WithImageTag\(ImageTag\)\s*'
               r'\.WithImageSHA256\(ImageDigest\);')
    if len(re.findall(pattern, source)) != 1 or len(re.findall(rf'\bAdd{method}\(', source)) != 1:
        raise ValueError('Expected one unchanged digest-pinned native database declaration')
    values = {}
    for name, value_pattern in (('DatabaseName', 'database'), ('ImageTag', '[a-zA-Z0-9][a-zA-Z0-9_.-]*'), ('ImageDigest', '[0-9a-f]{64}')):
        matches = re.findall(rf'\bconst string {name} = "({value_pattern})";', source)
        if len(matches) != 1 or len(re.findall(rf'\bconst string {name}\b', source)) != 1:
            raise ValueError('Expected one reviewed native image constant')
        values[name] = matches[0]
    tag, digest = values['ImageTag'], values['ImageDigest']
    return f'{repository}:{tag}@sha256:{digest}'


def prepare(provider, source):
    """Pull once, verify local immutable identity, and propagate every failure before tests."""
    reference = image_reference(provider, source)
    subprocess.run(['docker', 'pull', reference], check=True, timeout=PULL_SECONDS)
    result = subprocess.run(['docker', 'image', 'inspect', reference, '--format', '{{json .RepoDigests}}'],
                            check=True, timeout=INSPECT_SECONDS, capture_output=True, text=True)
    digests = json.loads(result.stdout)
    repository = PROVIDERS[provider][1]
    expected = repository + '@sha256:' + reference.rsplit('@sha256:', 1)[1]
    # Docker canonicalizes the default registry/library namespace when inspecting.
    accepted = {expected, expected.removeprefix('docker.io/library/')}
    if not isinstance(digests, list) or not all(isinstance(value, str) for value in digests) or not accepted.intersection(digests):
        raise ValueError('Prepared native image does not have its required repository digest')
    print(f'Prepared native image: {reference}; no test or startup deadline changed', flush=True)


def main():
    """Accept only the repository's two provider profiles; never start a container."""
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('provider', choices=PROVIDERS)
    args = parser.parse_args()
    prepare(args.provider, SETUPS[args.provider].read_text())


if __name__ == '__main__':
    main()
