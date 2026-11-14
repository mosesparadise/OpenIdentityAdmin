"""Run one bounded diagnostic case; failed findings and incomplete cohorts remain failures."""
import hashlib
import json
import os
from pathlib import Path
import re
import subprocess
import xml.etree.ElementTree as ET
from verify_contrast_experiment import execute
from test_evidence import require_complete_trx
from test_selection import catalogue, require_execution_names

root = Path(__file__).resolve().parents[1]
out = root/'artifacts/theme-subtree'
out.mkdir(parents=True, exist_ok=True)
project = root/'tests/aspire/IdentityPlatform.Issuer.Tests/IdentityPlatform.Issuer.Tests.csproj'
name = 'IdentityPlatform.Issuer.Tests.Features.Browser.ThemeSubtreeExperimentTests.Given_ShippedDarkCompactRecoveryLayout_When_ThemeOwnershipIsCompared_Then_AllFindingsRemainVisible'
expression = 'FullyQualifiedName=' + name
env = os.environ.copy()
env.update(IDENTITY_TEST_BROWSER='webkit', TESTINGPLATFORM_TELEMETRY_OPTOUT='1')
selection = catalogue(execute(['dotnet','run','--project',str(project),'--no-build','--no-restore','--','--pre-enumerate-theories','on','--list-tests','json','--filter',expression],env,timeout=60,capture=True))
assert len(selection)==1 and next(iter(selection.values()))[0]==name
paths = ['src/frontend/IdentityPlatform.DesignTokens/tokens.css','src/frontend/IdentityPlatform.DesignTokens/theme.js','src/backend/IdentityPlatform.AuthServer/Features/Accounts/Shared/login.css','src/backend/IdentityPlatform.AuthServer/Features/Accounts/Shared/login.js']
(out/'source.json').write_text(json.dumps({'commit':subprocess.check_output(['git','rev-parse','HEAD'],text=True).strip(),'tree':subprocess.check_output(['git','rev-parse','HEAD^{tree}'],text=True).strip(),'assets':{p:hashlib.sha256((root/p).read_bytes()).hexdigest() for p in paths},'scope':'synthetic diagnosis only; no product/provider/acceptance claim'},indent=2))
report=out/'theme-subtree.trx';report.unlink(missing_ok=True)
failure=None
try:
 execute(['dotnet','test','--project',str(project),'--no-build','--no-restore','--no-progress','--report-trx','--report-trx-filename',report.name,'--results-directory',str(out),'--pre-enumerate-theories','on','--filter',expression],env,timeout=660)
except (subprocess.CalledProcessError,RuntimeError) as error:
 failure=type(error).__name__
if report.exists():
 xml=ET.parse(report).getroot();results=xml.findall('.//{*}UnitTestResult')
 assert len(results)==1
 require_execution_names(report,selection)
 lines=[line.strip() for node in results[0].findall('./{*}Output/{*}StdOut') for line in (node.text or '').splitlines()]
 observations=[]
 for line in lines:
  match=re.fullmatch(r'THEME_SUBTREE sample=(\d+) arm=(OriginalNoScan|OwnedNoScan|OriginalFull|OwnedFull) step=(\d+) finding=(true|false) boundary=(full|final)',line)
  if match:
   sample,arm,step,finding,boundary=match.groups();observations.append({'sample':int(sample),'arm':arm,'step':int(step),'finding':finding=='true','boundary':boundary})
 expected={(i,a) for i in range(1,11) for a in ['OriginalNoScan','OwnedNoScan','OriginalFull','OwnedFull']}
 complete=len(observations)==40 and {(x['sample'],x['arm']) for x in observations}==expected
 count=sum(x['finding'] for x in observations)
 complete &= lines.count(f'THEME_SUBTREE_COMPLETE samples=10 arms=4 contexts=40 findings={count}')==1
 (out/'observations.json').write_text(json.dumps({'complete':complete,'findings':count,'observations':observations,'processFailure':failure,'acceptance':False},indent=2))
 assert complete,'Diagnostic cohort incomplete'
 require_complete_trx(report,expected_count=1)
else:
 raise RuntimeError('Diagnostic native report missing')
if failure: raise RuntimeError('Diagnostic process failed: '+failure)
print('Diagnostic cohort completed without findings; this is inconclusive for the original failure, not product acceptance.')
