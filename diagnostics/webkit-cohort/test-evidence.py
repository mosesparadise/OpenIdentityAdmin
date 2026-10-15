"""Pure controls for diagnostic validation; no browser or database execution."""
import copy
import json
from pathlib import Path
import subprocess
import unittest
from evidence import native, observation, ZERO, EXIT

NAME = 'Synthetic.Certificate(provider: "PostgreSql")'
SELECTED = {'native-uid': (NAME, 'PostgreSql', True, 'Synthetic')}

def trx(failed=False):
    counts = {key: 0 for key in ZERO}
    counts.update(total=1, executed=1, passed=int(not failed), failed=int(failed))
    counters = ' '.join(f'{key}="{value}"' for key, value in counts.items())
    outcome = 'Failed' if failed else 'Passed'
    name = NAME.replace('"','&quot;')
    error = '<Output><ErrorInfo><Message>synthetic cancelled</Message><StackTrace>Synthetic.Certificate()</StackTrace></ErrorInfo></Output>' if failed else ''
    info = '<RunInfos><RunInfo outcome="Error"><Text>' + EXIT + '</Text></RunInfo></RunInfos>' if failed else ''
    return f'''<TestRun><Results><UnitTestResult testName="{name}" testId="id" executionId="run" outcome="{outcome}">{error}</UnitTestResult></Results>
<TestDefinitions><UnitTest id="id" name="{name}"><Execution id="run"/></UnitTest></TestDefinitions>
<TestEntries><TestEntry testId="id" executionId="run"/></TestEntries>
<ResultSummary outcome="{outcome}"><Counters {counters}/>{info}</ResultSummary></TestRun>'''.encode()

def valid_observation():
    script = r'''
const {createObserver}=require('./observer.cjs');
const observer=createObserver(()=>1000000n);
const frame={_id:'frame'}, context={_closedStatus:'open',_browser:{isConnected:()=>true}};
function page(){return {_page:{mainFrame:()=>frame,isClosed:()=>false,initializedOrUndefined:()=>true},_session:{sessionId:'session'},_pageProxySession:{sessionId:'proxy'},_browserContext:context};}
const positive=page(), negative=page();
for(let n=1;n<=3;n++){
 const p=n===3?negative:positive,url='https://127.0.0.1:'+String(10000+n)+'/alive',loader='loader'+n,request='request'+n;
 observer.navigation(p,frame,url);observer.navigationResult(p,{loaderId:loader});
 function event(method,params){observer.protocol(p,{targetId:'session',message:JSON.stringify({method,params})});}
 event('Network.requestWillBeSent',{requestId:request,frameId:'frame',loaderId:loader,type:'Document',request:{url}});
 if(n<3)event('Network.responseReceived',{requestId:request,response:{status:200}});
 else event('Network.loadingFailed',{requestId:request,errorText:'cancelled'});
}
process.stdout.write(JSON.stringify(observer.snapshot()));
'''
    return json.loads(subprocess.check_output(['node','-e',script],cwd=Path(__file__).resolve().parent))

class EvidenceTests(unittest.TestCase):
    def test_pass(self): self.assertEqual(native(trx(),SELECTED,0)['outcome'],'Passed')
    def test_failed_native_case_remains_failure(self): self.assertEqual(native(trx(True),SELECTED,2)['outcome'],'Failed')
    def test_failure_wrong_exit_rejected(self):
        with self.assertRaises(AssertionError): native(trx(True),SELECTED,0)
    def test_contradictory_counters_rejected(self):
        with self.assertRaises(AssertionError): native(trx(True).replace(b'failed="1"',b'failed="0"'),SELECTED,2)
    def test_substituted_entry_rejected(self):
        with self.assertRaises(AssertionError): native(trx().replace(b'<TestEntry testId="id"',b'<TestEntry testId="other"'),SELECTED,0)
    def test_wrong_name_rejected(self):
        with self.assertRaises(AssertionError): native(trx(),{'x':('Other',)},0)
    def test_missing_failure_error_rejected(self):
        with self.assertRaises(AssertionError): native(trx(True).replace(b'<ErrorInfo>',b'<Other>').replace(b'</ErrorInfo>',b'</Other>'),SELECTED,2)
    def test_different_run_error_rejected(self):
        with self.assertRaises(AssertionError): native(trx(True).replace(EXIT.encode(),b'Unexpected run error'),SELECTED,2)
    def test_complete_cancellation_is_diagnostic_not_acceptance(self):
        result=observation(json.dumps(valid_observation()).encode())
        self.assertTrue(result['diagnostic_complete']);self.assertFalse(result['acceptance'])
    def test_bad_retention_states_never_complete(self):
        for key in ('instrumentationFailed','requestOverflow'):
            value=valid_observation();value[key]=True
            self.assertFalse(observation(json.dumps(value).encode())['diagnostic_complete'])
    def test_overflow_never_complete(self):
        value=valid_observation();value['dropped']=1;value['observed']+=1;value['retentionComplete']=False
        self.assertFalse(observation(json.dumps(value).encode())['diagnostic_complete'])
    def test_ambiguous_request_never_complete(self):
        value=valid_observation();value['phases'][2]['ambiguousRequestIdentity']=True
        self.assertFalse(observation(json.dumps(value).encode())['diagnostic_complete'])
    def test_missing_positive_never_complete(self):
        value=valid_observation()
        for row in value['phases'][0]['events']:
            if row['source']=='Network.responseReceived':row['status200']=False
        self.assertFalse(observation(json.dumps(value).encode())['diagnostic_complete'])
    def test_missing_binding_never_complete(self):
        value=valid_observation();value['phases'][2]['events'][-1]['binding']=None
        self.assertFalse(observation(json.dumps(value).encode())['diagnostic_complete'])
    def test_wrong_scope_never_complete(self):
        value=valid_observation();value['scopeMatches']=False
        self.assertFalse(observation(json.dumps(value).encode())['diagnostic_complete'])
    def test_unknown_field_rejected(self):
        value=valid_observation();value['phases'][2]['events'][-1]['rawUrl']='synthetic-secret'
        with self.assertRaises(AssertionError):observation(json.dumps(value).encode())
    def test_invalid_receipt_rejected(self):
        with self.assertRaises(ValueError):observation(b'invalid')
    def test_wrong_counter_types_rejected(self):
        for key in ('schema','limit','observed','dropped','extraNavigations','navigationCount'):
            value=valid_observation();value[key]=True
            with self.assertRaises(AssertionError):observation(json.dumps(value).encode())
        for key in ('navigation','matchingRequests','requestBindings'):
            value=valid_observation();value['phases'][0][key]='malformed'
            with self.assertRaises(AssertionError):observation(json.dumps(value).encode())
    def test_oversized_receipts_rejected(self):
        with self.assertRaises(AssertionError):observation(b' '*(128*1024+1))
        with self.assertRaises(AssertionError):native(b' '*(2*1024*1024+1),SELECTED,0)

if __name__=='__main__':unittest.main()
