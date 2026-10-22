#!/usr/bin/env python3
"""Select Ubuntu's official archive on disposable CI runners before browser provisioning."""
from pathlib import Path
import platform
import re
import subprocess
from urllib.parse import urlsplit, urlunsplit

REGIONAL = 'azure.archive.ubuntu.com'
OFFICIAL = 'archive.ubuntu.com'


def archive_uri(value):
    """Replace only the exact Ubuntu archive origin; reject ambiguous matching references."""
    if REGIONAL not in value.lower():
        return value
    uri = urlsplit(value)
    if (uri.scheme not in ('http', 'https') or uri.netloc != REGIONAL or
            uri.path not in ('/ubuntu', '/ubuntu/') or uri.query or uri.fragment):
        raise ValueError('Unexpected regional Ubuntu archive URI')
    return urlunsplit((uri.scheme, OFFICIAL, uri.path, '', ''))


def rewrite(source, deb822):
    """Preserve every byte except validated URI tokens in non-comment repository declarations."""
    result, field = [], None
    for line in source.splitlines(keepends=True):
        if not line.strip():
            field = None
        elif not line.lstrip().startswith('#'):
            if deb822:
                declaration = re.match(r'^([^\s:]+):', line)
                if declaration:
                    field = declaration[1].lower()
                elif not line[0].isspace():
                    field = None
                if REGIONAL in line.lower():
                    if field != 'uris':
                        raise ValueError('Regional archive outside a URIs field')
                    line = re.sub(r'\S+', lambda match: archive_uri(match[0]), line)
            elif REGIONAL in line.split('#', 1)[0].lower():
                declaration = re.match(r'^(deb(?:-src)?\s+(?:\[[^\]\r\n]+\]\s+)?)(\S+)(\s+.+)$', line)
                if not declaration:
                    raise ValueError('Malformed regional archive declaration')
                uri = archive_uri(declaration[2])
                if uri == declaration[2] or REGIONAL in (declaration[1] + declaration[3]).lower():
                    raise ValueError('Regional archive outside a repository URI')
                line = line[:declaration.start(2)] + uri + line[declaration.end(2):]
        result.append(line)
    return ''.join(result)


def prepare(root):
    """Validate all sources before writing; refresh signed indexes once after any replacement."""
    paths = sorted([*root.glob('sources.list'), *root.glob('sources.list.d/*.list'),
                    *root.glob('sources.list.d/*.sources')])
    if not paths:
        raise ValueError('Missing Ubuntu repository sources')
    changes = []
    for path in paths:
        if path.is_symlink() or not path.is_file():
            raise ValueError('Unexpected repository source file')
        original = path.read_bytes().decode('utf-8')
        updated = rewrite(original, path.suffix == '.sources')
        if updated != original:
            changes.append((path, updated))
    for path, updated in changes:
        path.write_bytes(updated.encode('utf-8'))
    if changes:
        subprocess.run(['apt-get', 'update'], check=True, timeout=120)
    print(f'Ubuntu browser provisioning: {len(changes)} source files moved to the official archive.')


def main():
    """Limit this explicit privileged setup command to the pinned Ubuntu runner release."""
    release = platform.freedesktop_os_release()
    if (release.get('ID'), release.get('VERSION_ID')) != ('ubuntu', '24.04'):
        raise ValueError('Browser archive preparation requires Ubuntu 24.04')
    prepare(Path('/etc/apt'))


if __name__ == '__main__':
    main()
