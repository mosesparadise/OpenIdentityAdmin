"""Exercise actual PowerShell helpers against a disposable fake Docker executable."""
import json
import os
from pathlib import Path
import shutil
import subprocess
import tempfile
import textwrap
import unittest

ROOT = Path(__file__).resolve().parents[1]
CONTAINER = 'a' * 12
RUN = 'b' * 32
FAKE = r'''#!/usr/bin/env python3
import json,os,sys,time
args=sys.argv[1:]
mode=os.environ.get('DEMO_TEST_MODE','success')
event={'args':args}
if args and args[0]=='exec':
    data=sys.stdin.buffer.read()
    event['eof']=True
    event['bom']=data.startswith(b'\xef\xbb\xbf')
    if 'initialize-administrator' in args:
        value=json.loads(data)
        event['fields']=sorted(value)
        event['bounded']=len(data)<=4096
        event['password_valid']=16<=len(value['password'])<=256
        event['unicode_preserved']=value['password'].endswith('λ')
        event['password_in_args']=any(value['password'] in a for a in args)
    if mode=='hang': time.sleep(10)
with open(os.environ['DEMO_TEST_LOG'],'a') as output: output.write(json.dumps(event)+'\n')
if args[0]=='ps':
    print('' if mode=='zero' else 'a'*12+'\n'+'c'*12 if mode=='multiple' else 'a'*12)
elif args[0]=='inspect': print('invalid' if mode=='ownership' else 'b'*32)
elif args[0]=='exec':
    if mode=='bootstrap-failure' and 'initialize-administrator' in args: sys.exit(2)
    if mode=='registration-failure' and '/app/register.sh' in args: sys.exit(2)
    print('Synthetic operation completed.')
'''


class LocalDemoHelpers(unittest.TestCase):
    """Assert process transport and mutation ordering without running a database."""

    def setUp(self):
        self.directory = tempfile.TemporaryDirectory(prefix='local-demo-helper-')
        self.root = Path(self.directory.name)
        self.log = self.root / 'events.jsonl'
        fake = self.root / 'docker'
        fake.write_text(FAKE)
        fake.chmod(0o700)
        self.environment = os.environ.copy()
        self.environment.update(PATH=str(self.root) + os.pathsep + os.environ['PATH'], DEMO_TEST_LOG=str(self.log))
        self.pwsh = os.environ.get('IDENTITY_PWSH') or shutil.which('pwsh')
        if not self.pwsh:
            self.fail('PowerShell 7 is required; helper verification cannot be skipped.')

    def tearDown(self):
        self.directory.cleanup()

    def invoke(self, mode='success', body=None):
        """Run production scripts with an in-memory prompt, retaining only non-sensitive fake process metadata."""
        self.environment['DEMO_TEST_MODE'] = mode
        script = self.root / 'invoke.ps1'
        default = '''
function Read-Host {
    param([string]$Prompt, [switch]$AsSecureString)
    if ($AsSecureString) { return ConvertTo-SecureString ([Guid]::NewGuid().ToString('N') + 'Aa1!λ') -AsPlainText -Force }
    return 'synthetic-admin'
}
& './scripts/local-demo/Initialize-LocalDemo.ps1'
'''
        script.write_text("$ErrorActionPreference = 'Stop'\n" + textwrap.dedent(body or default))
        result = subprocess.run([self.pwsh, '-NoProfile', '-File', str(script)], cwd=ROOT, env=self.environment,
                                capture_output=True, text=True, timeout=25)
        events = [json.loads(line) for line in self.log.read_text().splitlines()] if self.log.exists() else []
        return result, events

    def test_given_valid_run_when_initialized_then_utf8_eof_and_separate_operations_are_used(self):
        result, events = self.invoke()
        self.assertEqual(0, result.returncode, 'PowerShell initialization failed')
        commands = [event['args'][0] for event in events]
        self.assertEqual(['ps', 'inspect', 'exec', 'exec'], commands)
        bootstrap = events[2]
        self.assertEqual(['exec', '-i', CONTAINER, 'dotnet', '/app/IdentityPlatform.Migrator.dll', '--Operation', 'initialize-administrator'], bootstrap['args'])
        self.assertTrue(bootstrap['eof'] and bootstrap['bounded'] and bootstrap['password_valid'] and bootstrap['unicode_preserved'])
        self.assertFalse(bootstrap['bom'] or bootstrap['password_in_args'])
        self.assertEqual(['operationId', 'operatorReference', 'password', 'userName'], bootstrap['fields'])
        self.assertEqual(['exec', '-i', CONTAINER, '/app/register.sh'], events[3]['args'])

    def test_given_ambiguous_or_unowned_target_when_selected_then_no_mutation_runs(self):
        for mode in ('zero', 'multiple', 'ownership'):
            with self.subTest(mode=mode):
                self.log.unlink(missing_ok=True)
                result, events = self.invoke(mode)
                self.assertNotEqual(0, result.returncode)
                self.assertFalse(any(event['args'][0] in ('exec', 'stop', 'rm') for event in events))

    def test_given_mutation_failure_when_initializing_then_exact_owned_container_stops_without_retry(self):
        for mode, executions in (('bootstrap-failure', 1), ('registration-failure', 2)):
            with self.subTest(mode=mode):
                self.log.unlink(missing_ok=True)
                result, events = self.invoke(mode)
                self.assertNotEqual(0, result.returncode)
                self.assertEqual(executions, sum(event['args'][0] == 'exec' for event in events))
                self.assertEqual(['stop', '--time', '5', CONTAINER], events[-1]['args'])

    def test_given_hung_docker_when_deadline_expires_then_process_terminates_with_finite_error(self):
        result, _ = self.invoke('hang', '''
. './scripts/local-demo/DockerProcess.ps1'
Invoke-OwnedProcess -ProcessArguments @('exec', '-i', 'aaaaaaaaaaaa', '/app/register.sh') -TimeoutSeconds 1
''')
        self.assertNotEqual(0, result.returncode)
        self.assertIn('Docker command timed out', result.stderr)


if __name__ == '__main__':
    unittest.main()
