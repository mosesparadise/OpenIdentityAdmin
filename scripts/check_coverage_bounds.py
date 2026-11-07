"""Enforce original-report coverage bounds and exact current-run build provenance."""
import json
import os
from pathlib import Path
import sys
from check_coverage_inputs import expected_inputs, require_inputs
from coverage_bounds import Evidence, merged_line_map, read_report, require, statistics
from coverage_identity import ROOT, command, digest


def read_json(path):
    def unique(pairs):
        result = {}
        for key, value in pairs:
            require(key not in result, 'Duplicate JSON property: ' + key)
            result[key] = value
        return result
    return json.loads(path.read_text(), object_pairs_hook=unique)


def validate_identity(path, report, expected, modules):
    identity = read_json(path)
    require(set(identity) == {'schema', 'cell', 'build', 'reportSha256', 'observedModules', 'attestationLimit'}, 'Unexpected identity schema')
    require(type(identity['schema']) is int and identity['schema'] == 1 and
            identity['cell'] == report.name.removesuffix('.cobertura.xml'), 'Wrong coverage cell identity')
    require(identity['reportSha256'] == digest(report), 'Coverage report hash mismatch')
    build = identity['build']
    require(set(build) == {'commit', 'tree', 'runId', 'runAttempt', 'configuration', 'framework', 'sdk',
                          'collector', 'collectorSettingsSha256', 'collectorPackageSha256', 'toolManifestSha256', 'format', 'modules'}, 'Unexpected build schema')
    require(isinstance(build['sdk'], str) and build['sdk'].startswith('10.') and
            isinstance(build['collector'], str) and build['collector'].strip(), 'Missing SDK or collector identity')
    require(isinstance(build['collectorPackageSha256'], str) and len(build['collectorPackageSha256']) == 64 and
            all(c in '0123456789abcdef' for c in build['collectorPackageSha256']), 'Invalid collector package hash')
    for key, value in expected.items():
        require(build.get(key) == value, 'Coverage build mismatch: ' + key)
    require(identity['observedModules'] == sorted(modules), 'Coverage module manifest mismatch')
    return build


def evaluate(root, matrix, merged, expected, policy):
    require_inputs(root, matrix)
    paths = expected_inputs(root, matrix)
    require(set(root.rglob('*.cobertura.xml')) == set(paths), 'Unexpected or duplicated coverage input')
    manifests = {path.with_name(path.name.removesuffix('.cobertura.xml') + '.identity.json') for path in paths}
    require(set(root.rglob('*.identity.json')) == manifests, 'Missing or unexpected coverage identity')
    evidence, inputs, common = Evidence(), [], None
    for report in paths:
        raw = read_report(report)
        path = report.with_name(report.name.removesuffix('.cobertura.xml') + '.identity.json')
        build = validate_identity(path, report, expected, raw.modules)
        if common is None:
            common = build
        require(build == common, 'Coverage inputs use different builds or collector settings')
        require(set(build['modules']) == set(policy['requiredModules']), 'Build manifest must bind every required module')
        for module, hashes in build['modules'].items():
            require(set(hashes) == {'dllSha256', 'pdbSha256'}, 'Missing DLL/PDB binding: ' + module)
            for value in hashes.values():
                require(isinstance(value, str) and len(value) == 64 and all(c in '0123456789abcdef' for c in value), 'Invalid build hash')
        evidence.add(raw)
        inputs.append({'cell': report.name.removesuffix('.cobertura.xml'), 'reportSha256': digest(report),
                       'identitySha256': digest(path)})
    require((evidence.modules, evidence.lines) == merged_line_map(merged), 'Merged line union differs from original evidence')
    return {'schema': 1, 'build': common, 'inputs': inputs, 'modules': statistics(evidence, policy),
            'mergedReportSha256': digest(merged), 'branchMethod': 'Conservative lower bound from original method conditions; repeated partial outcomes are not inferred.',
            'limits': ['Upstream merged branch metadata is lossy and is not authoritative.',
                       'Build provenance binds trusted CI output copies, not runtime loaded-module attestation.',
                       'Floors apply to the unchanged required-module policy; other instrumented drivers have no floor.']}


def main():
    root, matrix, merged, output = Path(sys.argv[1]), json.loads(sys.argv[2]), Path(sys.argv[3]), Path(sys.argv[4])
    tools = read_json(ROOT / 'dotnet-tools.json')
    expected = {'commit': command('git', 'rev-parse', 'HEAD'), 'tree': command('git', 'rev-parse', 'HEAD^{tree}'),
                'runId': os.environ['GITHUB_RUN_ID'], 'runAttempt': os.environ['GITHUB_RUN_ATTEMPT'],
                'configuration': 'Debug', 'framework': 'net10.0', 'format': 'cobertura',
                'collectorSettingsSha256': digest(ROOT / 'eng/coverage.xml'), 'toolManifestSha256': digest(ROOT / 'dotnet-tools.json')}
    result = evaluate(root, matrix, merged, expected, read_json(ROOT / 'eng/coverage-policy.json'))
    require(result['build']['collector'].split('+')[0] == tools['tools']['dotnet-coverage']['version'], 'Unexpected collector')
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + '\n')
    for row in result['modules']:
        print(f"PASS {row['module']}: lines {row['linesCovered']}/{row['linesTotal']}; branch lower bound {row['branchesCoveredLowerBound']}/{row['branchesTotal']}")


if __name__ == '__main__':
    main()
