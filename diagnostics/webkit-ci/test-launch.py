"""Exercise the exact workflow guard with independent accepted/rejected event fixtures."""
import ast
import copy
from pathlib import Path
import unittest
import yaml

local_workflow = Path(__file__).parent/'verify.yml'
workflow_path = local_workflow if local_workflow.exists() else Path(__file__).parent/'launched-verify.yml'
workflow = yaml.safe_load(workflow_path.read_text())
step = workflow['jobs']['diagnostic']['steps'][0]['run']
python = step.split("python3 - <<'PY'\n", 1)[1].rsplit('\nPY', 1)[0]
syntax = ast.parse(python)
definition = next(node for node in syntax.body if isinstance(node, ast.FunctionDef) and node.name == 'validate_launch')
scope = {}; exec('import re', scope)
exec(compile(ast.Module(body=[definition], type_ignores=[]), 'workflow guard', 'exec'), scope)
validate = scope['validate_launch']
PARENT = 'ecd3009b4d99370a958f154f1945a0b32a216170'
AFTER = 'b'*40


class LaunchTests(unittest.TestCase):
    def setUp(self):
        self.env = {'GITHUB_EVENT_NAME':'push','GITHUB_REPOSITORY':'mosesparadise/OpenIdentityAdmin',
                    'GITHUB_REF':'refs/heads/diagnostic/webkit-ci-provenance','GITHUB_RUN_ATTEMPT':'1',
                    'RUNNER_ENVIRONMENT':'github-hosted','GITHUB_SHA':AFTER}
        self.event = {'repository':{'full_name':'mosesparadise/OpenIdentityAdmin'},
                      'ref':self.env['GITHUB_REF'],'before':PARENT,'after':AFTER,'head_commit':{'id':AFTER},
                      'created':False,'deleted':False,'forced':False}

    def test_exact_authorized_transition_accepted(self): validate(self.event,self.env)

    def test_other_events_and_refs_rejected(self):
        for key,value in [('GITHUB_EVENT_NAME','workflow_dispatch'),('GITHUB_EVENT_NAME','pull_request'),
                          ('GITHUB_REF','refs/heads/main'),('GITHUB_REPOSITORY','other/project'),
                          ('RUNNER_ENVIRONMENT','self-hosted')]:
            env={**self.env,key:value}
            with self.assertRaises(AssertionError): validate(self.event,env)

    def test_rerun_rejected(self):
        with self.assertRaises(AssertionError): validate(self.event,{**self.env,'GITHUB_RUN_ATTEMPT':'2'})

    def test_later_push_and_reset_rejected(self):
        for parent in (AFTER,'0'*40,'c'*40):
            with self.assertRaises(AssertionError): validate({**self.event,'before':parent},self.env)

    def test_created_deleted_forced_and_malformed_flags_rejected(self):
        for key in ('created','deleted','forced'):
            for value in (True,0,'false',None):
                with self.assertRaises(AssertionError): validate({**self.event,key:value},self.env)

    def test_mismatched_commit_rejected(self):
        for event in ({**self.event,'after':'c'*40},{**self.event,'head_commit':{'id':'c'*40}}):
            with self.assertRaises(AssertionError): validate(event,self.env)

    def test_event_repository_and_ref_must_match(self):
        for event in ({**self.event,'ref':'refs/heads/main'},{**self.event,'repository':{'full_name':'other/project'}}):
            with self.assertRaises(AssertionError): validate(event,self.env)

    def test_invalid_sha_and_noop_rejected(self):
        for value in ('x'*40,'a'*39,'a'*41,PARENT):
            event={**self.event,'after':value,'head_commit':{'id':value}}
            with self.assertRaises(AssertionError): validate(event,{**self.env,'GITHUB_SHA':value})

    def test_missing_event_evidence_rejected(self):
        for key in self.event:
            event=copy.deepcopy(self.event);del event[key]
            with self.assertRaises((KeyError,AssertionError)): validate(event,self.env)

    def test_only_one_narrow_trigger_and_one_job(self):
        trigger=workflow.get('on',workflow.get(True))
        self.assertEqual(trigger,{'push':{'branches':['diagnostic/webkit-ci-provenance']}})
        self.assertEqual(set(workflow['jobs']),{'diagnostic'})


if __name__=='__main__': unittest.main()
