#!/usr/bin/env python3
"""Export real host contracts and prove configured metadata under audited, bounded native execution."""
import argparse
import json
import os
import re
from pathlib import Path
import tempfile
from verify_contrast_experiment import execute, install_cancellation_handler

ROOT = Path(__file__).resolve().parents[1]
SURFACES = (('Api', 'v1'), ('Bff', 'bff-v1'))
EXPORT_TIMEOUT_SECONDS = 60
NATIVE_TOOL_PACKAGE = 'Microsoft.Extensions.ApiDescription.Server'
SYNTHETIC_DATABASE = 'Host=127.0.0.1;Port=1;Database=contract_export;Username=synthetic;Password=synthetic'
PROBE_TITLE = 'Configured native export probe'
PROBE_DESCRIPTION = 'Synthetic environment override proves native configuration reaches generated metadata.'


def require_no_socket_effects(lines):
    """Permit only runtime UDP/Unix descriptor probes; reject every network operation, including failed ones."""
    allocation = re.compile(r'^\d+\s+socket\((AF_INET6?|AF_UNIX), SOCK_DGRAM\|SOCK_CLOEXEC, (IPPROTO_IP|0)\)\s+= \d+$')
    if any(not allocation.fullmatch(line.strip()) for line in lines):
        raise ValueError('Contract export attempted an unapproved socket effect')


def export_environment(surface):
    """Select the real host's settings independently of caller roots, retaining ordinary metadata override precedence."""
    environment = os.environ.copy()
    project = ROOT / f'src/backend/IdentityPlatform.Admin.{surface}'
    environment.update(Database__Provider='PostgreSql', ConnectionStrings__platform=SYNTHETIC_DATABASE,
                       ConnectionStrings__adminsessions=SYNTHETIC_DATABASE,
                       DOTNET_ENVIRONMENT='Production', ASPNETCORE_ENVIRONMENT='Production',
                       ASPNETCORE_CONTENTROOT=str(project), DOTNET_CONTENTROOT=str(project),
                       DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_EnableDiagnostics='0')
    return environment


def export_command(surface, directory, trace):
    """Build an argument-array invocation of the pinned native exporter and network syscall tracer."""
    cache = Path(os.environ.get('NUGET_PACKAGES', str(Path.home() / '.nuget/packages')))
    project = ROOT / f'src/backend/IdentityPlatform.Admin.{surface}'
    assembly = project / f'bin/Debug/net10.0/IdentityPlatform.Admin.{surface}.dll'
    tool = native_tool_path(project)
    return ['strace', '-f', '-qq', '-e', 'trace=%network', '-e', 'signal=none', '-o', str(trace),
            'dotnet', str(cache / tool), '--assembly', str(assembly),
            '--file-list', str(directory / 'files.txt'), '--framework', '.NETCoreApp,Version=v10.0',
            '--output', str(directory), '--project', f'IdentityPlatform.Admin.{surface}',
            '--assets-file', str(project / 'obj/project.assets.json'), '--platform', 'AnyCPU']


def native_tool_path(project):
    """Resolve the exporter from this host's exact checked-in lock, never an older incidental cache entry."""
    locked = json.loads((project / 'packages.lock.json').read_text())
    version = locked['dependencies']['net10.0'][NATIVE_TOOL_PACKAGE]['resolved']
    if not isinstance(version, str) or not re.fullmatch(r'[0-9]+\.[0-9]+\.[0-9]+', version):
        raise ValueError('Native exporter requires a locked stable package version')
    return Path(NATIVE_TOOL_PACKAGE.lower()) / version / 'tools/dotnet-getdocument.dll'


def export_once(surface, environment):
    """Invoke the actual entry point without a listener and return its validated document plus socket allocation count."""
    with tempfile.TemporaryDirectory(prefix='identity-contract-') as temporary:
        directory = Path(temporary)
        trace = directory / 'connect.trace'
        try:
            execute(export_command(surface, directory, trace), environment, timeout=EXPORT_TIMEOUT_SECONDS, capture=True)
        finally:
            lines = trace.read_text().splitlines()
            require_no_socket_effects(lines)
        files = [Path(line) for line in (directory / 'files.txt').read_text().splitlines() if line.strip()]
        if len(files) != 1:
            raise ValueError('Export must have exactly one actual-host document')
        document = json.loads(files[0].read_text())
        if document['openapi'] != '3.1.1' or document['paths'] != {} or document['servers'] != [{'url': '/'}]:
            raise ValueError('Actual host contract must truthfully contain zero management operations')
        return document, len(lines)


def verify_metadata_override(surface, environment):
    """Prove native generation reads configuration rather than matching appsettings with hardcoded transformer text."""
    override = dict(environment, ApiContracts__Metadata__Title=PROBE_TITLE, ApiContracts__Metadata__Description=PROBE_DESCRIPTION)
    document, allocations = export_once(surface, override)
    if document['info']['title'] != PROBE_TITLE or document['info']['description'] != PROBE_DESCRIPTION:
        raise ValueError('Native metadata ignored the configuration override')
    return {'surface': surface, 'control': 'configured-metadata', 'socketEffects': 0,
            'socketAllocations': allocations, 'operations': 0}


def export(destination):
    """Require deterministic ordinary output and a separate configuration probe for each shipped host."""
    destination.mkdir(parents=True, exist_ok=True)
    evidence = []
    for surface, document_name in SURFACES:
        environment = export_environment(surface)
        outputs = []
        for iteration in range(2):
            document, allocations = export_once(surface, environment)
            outputs.append(json.dumps(document, sort_keys=True, indent=2) + '\n')
            evidence.append({'surface': surface, 'iteration': iteration + 1, 'socketEffects': 0,
                             'socketAllocations': allocations, 'operations': 0})
        if outputs[0] != outputs[1]:
            raise ValueError('Actual-host generation is not deterministic')
        evidence.append(verify_metadata_override(surface, environment))
        (destination / (document_name + '.json')).write_text(outputs[0])
    (destination / 'export-evidence.json').write_text(json.dumps(evidence, indent=2) + '\n')


if __name__ == '__main__':
    install_cancellation_handler()
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/contracts/actual')
    args = parser.parse_args()
    export(args.output)
    print('Verified both actual entry points: repeatable 3.1 inventories, configured metadata, zero operations/network effects.')
