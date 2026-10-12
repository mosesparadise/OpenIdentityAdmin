#!/usr/bin/env python3
"""Export actual host metadata twice, with synthetic inputs and audited socket effects."""
import argparse
import json
import os
import re
from pathlib import Path
import tempfile
from verify_contrast_experiment import execute, install_cancellation_handler

ROOT = Path(__file__).resolve().parents[1]


def require_no_socket_effects(lines):
    # The runtime probes UDP/Unix socket availability by allocating descriptors.
    # No bind/listen/connect/send/receive/option operations are allowed, even failed ones.
    allocation = re.compile(r'^\d+\s+socket\((AF_INET6?|AF_UNIX), SOCK_DGRAM\|SOCK_CLOEXEC, (IPPROTO_IP|0)\)\s+= \d+$')
    if any(not allocation.fullmatch(line.strip()) for line in lines):
        raise ValueError('Contract export attempted an unapproved socket effect')


def export(destination):
    destination.mkdir(parents=True, exist_ok=True)
    cache = Path(os.environ.get('NUGET_PACKAGES', str(Path.home() / '.nuget/packages')))
    tool = cache / 'microsoft.extensions.apidescription.server/10.0.12/tools/dotnet-getdocument.dll'
    environment = os.environ.copy()
    environment.update(Database__Provider='PostgreSql',
                       ConnectionStrings__platform='Host=127.0.0.1;Port=1;Database=contract_export;Username=synthetic;Password=synthetic',
                       ConnectionStrings__adminsessions='Host=127.0.0.1;Port=1;Database=contract_export;Username=synthetic;Password=synthetic',
                       DOTNET_ENVIRONMENT='Production', ASPNETCORE_ENVIRONMENT='Production',
                       DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_EnableDiagnostics='0')
    # Export is a build tool invoking the real entry point; no service/network side effects are allowed.
    evidence = []
    for surface, document_name in [('Api', 'v1'), ('Bff', 'bff-v1')]:
        project = ROOT / f'src/backend/IdentityPlatform.Admin.{surface}'
        assembly = project / f'bin/Debug/net10.0/IdentityPlatform.Admin.{surface}.dll'
        outputs = []
        for iteration in range(2):
            with tempfile.TemporaryDirectory(prefix='identity-contract-') as temporary:
                directory = Path(temporary)
                trace = directory / 'connect.trace'
                command = ['strace', '-f', '-qq', '-e', 'trace=%network', '-e', 'signal=none', '-o', str(trace),
                           'dotnet', str(tool), '--assembly', str(assembly),
                           '--file-list', str(directory / 'files.txt'), '--framework', '.NETCoreApp,Version=v10.0',
                           '--output', str(directory), '--project', f'IdentityPlatform.Admin.{surface}',
                           '--assets-file', str(project / 'obj/project.assets.json'), '--platform', 'AnyCPU']
                execute(command, environment, timeout=60, capture=True)
                lines = trace.read_text().splitlines()
                require_no_socket_effects(lines)
                files = [Path(line) for line in (directory / 'files.txt').read_text().splitlines() if line.strip()]
                if len(files) != 1:
                    raise ValueError('Export must have exactly one actual-host document')
                document = json.loads(files[0].read_text())
                if document['openapi'] != '3.1.1' or document['paths'] != {} or document['servers'] != [{'url': '/'}]:
                    raise ValueError('Actual host contract must truthfully contain zero management operations')
                outputs.append(json.dumps(document, sort_keys=True, indent=2) + '\n')
                evidence.append({'surface': surface, 'iteration': iteration + 1, 'socketEffects': 0, 'socketAllocations': len(lines), 'operations': 0})
        if outputs[0] != outputs[1]:
            raise ValueError('Actual-host generation is not deterministic')
        (destination / (document_name + '.json')).write_text(outputs[0])
    (destination / 'export-evidence.json').write_text(json.dumps(evidence, indent=2) + '\n')


if __name__ == '__main__':
    install_cancellation_handler()
    parser = argparse.ArgumentParser()
    parser.add_argument('--output', type=Path, default=ROOT / 'artifacts/contracts/actual')
    args = parser.parse_args()
    export(args.output)
    print('Verified both actual entry points: repeatable 3.1 inventories, zero operations/network connections.')
