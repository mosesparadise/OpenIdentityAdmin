"""Native cohort controls cover failures, identity substitution and historical-order limits."""
import copy
import datetime
import json
from pathlib import Path
import unittest
import xml.etree.ElementTree as ET
from cohort import native, TARGET
from evidence import ZERO, EXIT

HISTORY = json.loads((Path(__file__).parent/'historical-cohort.json').read_text())
NAMES = [r['name'] for r in HISTORY['native_order']]
SELECTED = {str(i):(name,'PostgreSql',True,'Browser') for i,name in enumerate(NAMES)}


def trx(failures=(), reverse=False):
    root=ET.Element('TestRun'); results=ET.SubElement(root,'Results')
    definitions=ET.SubElement(root,'TestDefinitions'); entries=ET.SubElement(root,'TestEntries')
    names=list(reversed(NAMES)) if reverse else NAMES
    for i,name in enumerate(names):
        failed=name in failures
        start=f'2026-10-15T10:{i:02}:00.0000000+00:00'; end=f'2026-10-15T10:{i:02}:01.0000000+00:00'
        row=ET.SubElement(results,'UnitTestResult',testId=str(i),executionId='e'+str(i),testName=name,
            outcome='Failed' if failed else 'Passed',startTime=start,endTime=end,duration='00:00:01.0000000')
        definition=ET.SubElement(definitions,'UnitTest',id=str(i),name=name)
        ET.SubElement(definition,'Execution',id='e'+str(i)); ET.SubElement(entries,'TestEntry',testId=str(i),executionId='e'+str(i))
        if failed:
            error=ET.SubElement(ET.SubElement(row,'Output'),'ErrorInfo')
            ET.SubElement(error,'Message').text='private synthetic failure'
            ET.SubElement(error,'StackTrace').text='CertificateObservation.RequireAsync' if name==TARGET else 'private.Other'
    summary=ET.SubElement(root,'ResultSummary',outcome='Failed' if failures else 'Completed')
    counts={k:'0' for k in ZERO}; counts.update(total='30',executed='30',passed=str(30-len(failures)),failed=str(len(failures)))
    ET.SubElement(summary,'Counters',counts)
    if failures: ET.SubElement(ET.SubElement(ET.SubElement(summary,'RunInfos'),'RunInfo',outcome='Error'),'Text').text=EXIT
    return root


class CohortTests(unittest.TestCase):
    def validate(self,root,exit_code=0): return native(ET.tostring(root),SELECTED,exit_code,HISTORY)

    def test_complete_pass_and_exact_order(self):
        r=self.validate(trx()); self.assertEqual(r['certificate_ordinal'],20)
        self.assertTrue(r['historical_order_matches']); self.assertTrue(r['historical_prefix_matches'])
        self.assertEqual(r['counts']['passed'],30); self.assertFalse(r['acceptance'])

    def test_native_certificate_failure_remains_failed(self):
        r=self.validate(trx((TARGET,)),2); self.assertEqual(r['outcome'],'Failed')
        self.assertTrue(r['certificate_failure']['certificate_wait_frame'])
        self.assertNotIn('private synthetic',json.dumps(r))

    def test_other_and_multiple_failures_remain_failed(self):
        for failures in ((NAMES[0],),(TARGET,NAMES[0])):
            r=self.validate(trx(failures),2); self.assertEqual(r['counts']['failed'],len(failures))
            self.assertNotIn('private.Other',json.dumps(r))

    def test_changed_order_not_claimed_historical(self):
        r=self.validate(trx(reverse=True)); self.assertFalse(r['historical_order_matches'])
        self.assertFalse(r['historical_prefix_matches']); self.assertEqual(r['certificate_ordinal'],11)

    def test_wrong_exit_and_counters_rejected(self):
        with self.assertRaises(AssertionError): self.validate(trx((TARGET,)),0)
        for key,value in [('failed','1'),('passed','29'),('notExecuted','1'),('executed','29')]:
            r=trx(); r.find('./ResultSummary/Counters').set(key,value)
            with self.assertRaises(AssertionError): self.validate(r)

    def test_missing_duplicate_and_substituted_rows_rejected(self):
        for mode in ('missing','duplicate','wrong-name','wrong-entry','wrong-definition'):
            r=trx(); rows=r.find('Results')
            if mode=='missing': rows.remove(rows[0])
            if mode=='duplicate': rows[0].set('testName',rows[1].get('testName'))
            if mode=='wrong-name': rows[0].set('testName','private unknown')
            if mode=='wrong-entry': r.find('./TestEntries/TestEntry').set('executionId','wrong')
            if mode=='wrong-definition': r.find('./TestDefinitions/UnitTest').set('name','wrong')
            with self.assertRaises(AssertionError): self.validate(r)

    def test_wrong_timing_and_overlap_rejected(self):
        for value in ('unknown','2026-10-15T10:02:00.0000000+00:00'):
            r=trx(); r.find('./Results/UnitTestResult').set('endTime',value)
            with self.assertRaises(AssertionError): self.validate(r)

    def test_reversed_submicrosecond_timing_rejected(self):
        r=trx(); row=r.find('./Results/UnitTestResult')
        row.set('startTime','2026-10-15T10:00:00.0000002+00:00')
        row.set('endTime','2026-10-15T10:00:00.0000001+00:00')
        with self.assertRaises(AssertionError): self.validate(r)

    def test_missing_failure_details_and_unexpected_runinfo_rejected(self):
        r=trx((TARGET,)); error=r.find('.//ErrorInfo'); error.remove(error.find('StackTrace'))
        with self.assertRaises(AssertionError): self.validate(r,2)
        r=trx(); ET.SubElement(r,'RunInfo',outcome='Warning')
        with self.assertRaises(AssertionError): self.validate(r)

    def test_skipped_row_and_dtd_rejected(self):
        r=trx(); r.find('./Results/UnitTestResult').set('outcome','NotExecuted')
        with self.assertRaises(AssertionError): self.validate(r)
        with self.assertRaises(AssertionError): native(b'<!DOCTYPE x>'+ET.tostring(trx()),SELECTED,0,HISTORY)


if __name__=='__main__': unittest.main()
