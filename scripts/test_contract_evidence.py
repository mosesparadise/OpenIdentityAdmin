import copy
import json
from pathlib import Path
import tempfile
import unittest
from contract_evidence import catalogue, require_node_report
from export_api_contracts import require_no_socket_effects, native_tool_path


class ContractEvidenceTests(unittest.TestCase):
    def test_native_exporter_tracks_locked_upgrades_and_rejects_unsafe_versions(self):
        with tempfile.TemporaryDirectory() as directory:
            project = Path(directory)
            for version in ['10.0.14', '10.0.15', '../10.0.12', '10.0.14-preview.1', None]:
                lock = {'dependencies': {'net10.0': {'Microsoft.Extensions.ApiDescription.Server': {'resolved': version}}}}
                (project / 'packages.lock.json').write_text(json.dumps(lock))
                if version in ('10.0.14', '10.0.15'):
                    self.assertEqual(f'microsoft.extensions.apidescription.server/{version}/tools/dotnet-getdocument.dll', native_tool_path(project).as_posix())
                else:
                    with self.assertRaises(ValueError): native_tool_path(project)

    def test_discovery_rejects_missing_duplicate_and_misclassified_identity(self):
        case = {'uid': 'a', 'displayName': 'VersionSelectionTests.Valid'}
        correct = {'schemaVersion': 1, 'tests': [case]}
        self.assertEqual({'a': ('VersionSelectionTests.Valid',)}, catalogue(json.dumps(correct)))
        changes = [{'schemaVersion': 2, 'tests': [case]}, {'schemaVersion': 1, 'tests': []},
                   {'schemaVersion': 1, 'tests': [case, case]}]
        for key, value in [('uid', ''), ('displayName', ''), ('displayName', 'ViewerTests.Case'),
                           ('traits', [{'key': 'Category', 'value': 'Browser'}])]:
            candidate = copy.deepcopy(correct); candidate['tests'][0][key] = value; changes.append(candidate)
        for candidate in changes:
            with self.assertRaises(ValueError): catalogue(json.dumps(candidate))

    def test_node_report_requires_exact_cases_and_summary(self):
        correct = '<testsuites><testcase name="A"/><!-- tests 1 --><!-- suites 0 --><!-- pass 1 --><!-- fail 0 --><!-- cancelled 0 --><!-- skipped 0 --><!-- todo 0 --></testsuites>'
        with tempfile.TemporaryDirectory() as directory:
            path = Path(directory) / 'node.xml'
            path.write_text(correct); require_node_report(path, ['A'])
            changes = [correct.replace('name="A"', 'name="B"'), correct.replace('name="A"', 'name=""'),
                       correct.replace('<testcase name="A"/>', ''),
                       correct.replace('<testcase name="A"/>', '<testcase name="A"/><testcase name="A"/>'),
                       correct.replace('<testcase name="A"/>', '<testcase name="A"><skipped/></testcase>')]
            for key, value in [('tests', 1), ('suites', 0), ('pass', 1), ('fail', 0), ('cancelled', 0), ('skipped', 0), ('todo', 0)]:
                marker = f'<!-- {key} {value} -->'
                changes += [correct.replace(marker, ''), correct.replace(marker, marker * 2),
                            correct.replace(marker, f'<!-- {key} {value + 1} -->')]
            for candidate in changes:
                path.write_text(candidate)
                with self.assertRaises(ValueError): require_node_report(path, ['A'])

    def test_socket_trace_allows_only_runtime_descriptor_allocation(self):
        require_no_socket_effects([])
        require_no_socket_effects(['123 socket(AF_INET6, SOCK_DGRAM|SOCK_CLOEXEC, IPPROTO_IP) = 5'])
        for operation in ['connect(5, {sa_family=AF_INET}, 16)', 'connect(5, {sa_family=AF_UNIX}, 16)',
                          'bind(5, {sa_family=AF_INET}, 16)', 'listen(5, 10)', 'sendto(5, "x", 1, 0, NULL, 0)',
                          'sendmsg(5, {}, 0)', 'recvfrom(5, "x", 1, 0, NULL, NULL)',
                          'socket(AF_INET, SOCK_STREAM, IPPROTO_TCP)', 'unknown(5)']:
            with self.assertRaises(ValueError): require_no_socket_effects([f'123 {operation} = -1 EPERM'])


if __name__ == '__main__':
    unittest.main()
