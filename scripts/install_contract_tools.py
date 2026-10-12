#!/usr/bin/env python3
"""Install the reviewed Linux AMD64 diff binary after archive integrity validation."""
import hashlib
import io
from pathlib import Path
import tarfile
import urllib.request

ROOT = Path(__file__).resolve().parents[1]
URL = 'https://github.com/oasdiff/oasdiff/releases/download/v1.33.0/oasdiff_1.33.0_linux_amd64.tar.gz'
SHA256 = '43a4e328e2d13ba1552d760aa68d2485c75c5621f309f6ff64ae895188345247'


def install():
    target = ROOT / 'artifacts/tools'
    target.mkdir(parents=True, exist_ok=True)
    archive = target / 'oasdiff.tar.gz'
    if not archive.exists():
        with urllib.request.urlopen(URL, timeout=60) as response:
            archive.write_bytes(response.read())
    data = archive.read_bytes()
    if hashlib.sha256(data).hexdigest() != SHA256:
        raise ValueError('oasdiff archive integrity mismatch')
    with tarfile.open(fileobj=io.BytesIO(data), mode='r:gz') as package:
        source = package.extractfile('oasdiff')
        if source is None:
            raise ValueError('Pinned archive is missing the binary')
        binary = target / 'oasdiff'
        binary.write_bytes(source.read())
        binary.chmod(0o755)
    print('Installed pinned oasdiff1.33.0 after SHA256 validation.')


if __name__ == '__main__':
    install()
