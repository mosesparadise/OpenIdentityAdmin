"""Reconcile one passive observation without turning a native failure into acceptance."""
import re
import xml.etree.ElementTree as ET

LIMIT = 1024 * 1024
PHASES = ('Setup', 'Submit', 'Safe', 'Back', 'Controls', 'Complete')
ROUTE = r'(Form|Post|Safe|NoContent|Other)'
EVENT = re.compile(r'history-navigation ordinal=(\d{1,2}) elapsed-ms=(\d{1,6}) phase=(' + '|'.join(PHASES) + r') (.+)')
RESPONSE = re.compile(r'response main=(True|False) navigation=(True|False) route=' + ROUTE + r' method=(Get|Post|Other) status=(Ok|NoContent|Other)')
COMMIT = re.compile(r'commit main=(True|False) route=' + ROUTE)
CATCH = re.compile(r'catch responses=(\d{1,4}) commits=(\d{1,4})')
FINAL = re.compile(r'history-navigation-final overflow=False posts=(\d{1,4})')
NATIVE_EXIT_TWO = "Exit code indicates failure: '2'. Please refer to https://aka.ms/testingplatform/exitcodes for more information."


def require_ledger(lines, outcome):
    """Reject absent, truncated, substituted or contradictory event records for either native outcome."""
    records = [line.strip() for line in lines if line.strip().startswith('history-navigation')]
    if not 2 <= len(records) <= 65 or not (final := FINAL.fullmatch(records[-1])):
        raise ValueError('Missing, overflowed or malformed navigation evidence')
    posts = int(final[1])
    if posts >= 1000:
        raise ValueError('POST counter saturated')
    prior_time = prior_phase = 0
    catches = []
    setup = set()
    back_responses = back_commits = 0
    for ordinal, line in enumerate(records[:-1], 1):
        if not (event := EVENT.fullmatch(line)) or int(event[1]) != ordinal:
            raise ValueError('Invalid navigation ordinal or record')
        elapsed, phase, detail = int(event[2]), PHASES.index(event[3]), event[4]
        if not prior_time <= elapsed <= 180_000 or phase < prior_phase:
            raise ValueError('Invalid navigation phase or clock order')
        prior_time, prior_phase = elapsed, phase
        if match := CATCH.fullmatch(detail):
            if phase != PHASES.index('Back') or max(map(int, match.groups())) >= 1000:
                raise ValueError('Invalid classifier snapshot')
            counts = tuple(map(int, match.groups()))
            if counts[0] > back_responses or counts[1] > back_commits:
                raise ValueError('Classifier counts lack preceding passive event evidence')
            catches.append(counts)
        elif match := RESPONSE.fullmatch(detail):
            if match.groups()[:2] == ('True', 'True'):
                setup.add(('response', event[3], match[3], match[4], match[5]))
                back_responses += int(event[3] == 'Back')
        elif match := COMMIT.fullmatch(detail):
            if match[1] == 'True':
                setup.add(('commit', event[3], match[2]))
                back_commits += int(event[3] == 'Back')
        elif detail != 'phase':
            raise ValueError('Unrecognized navigation category')
    expected_setup = {('response', phase, route, method, 'Ok') for phase, route, method in
                      [('Setup', 'Form', 'Get'), ('Submit', 'Post', 'Post'), ('Safe', 'Safe', 'Get')]}
    expected_setup |= {('commit', phase, route) for phase, route in [('Setup', 'Form'), ('Submit', 'Post'), ('Safe', 'Safe')]}
    if not expected_setup <= setup:
        raise ValueError('Setup navigation evidence is incomplete')
    if len(catches) != 1:
        raise ValueError('Expected exactly one native cache-miss snapshot')
    if outcome == 'Passed' and (catches[0] != (0, 0) or posts != 1 or prior_phase != PHASES.index('Complete')):
        raise ValueError('Passing control contradicts its navigation evidence')
    if outcome == 'Failed' and (catches[0] == (0, 0) or prior_phase != PHASES.index('Back')):
        raise ValueError('Failed classifier evidence does not match this observation')
    return {'events': len(records) - 1, 'responses_at_catch': catches[0][0],
            'commits_at_catch': catches[0][1], 'posts_before_cleanup': posts}


def require_receipt(path, name, exit_code):
    """Require exact native counters/result/definition/execution bindings while preserving Passed versus Failed."""
    if not path.is_file() or path.stat().st_size > LIMIT:
        raise ValueError('Missing or oversized observation report')
    root = ET.parse(path).getroot()
    rows = root.findall('./{*}Results/{*}UnitTestResult')
    definitions = root.findall('./{*}TestDefinitions/{*}UnitTest')
    entries = root.findall('./{*}TestEntries/{*}TestEntry')
    summaries = root.findall('./{*}ResultSummary')
    counters = root.findall('./{*}ResultSummary/{*}Counters')
    if any(len(values) != 1 for values in (rows, definitions, entries, summaries, counters)):
        raise ValueError('Observation must contain exactly one bound native result')
    row, definition, entry, summary, counter = rows[0], definitions[0], entries[0], summaries[0], counters[0]
    outcome = row.get('outcome')
    if outcome not in ('Passed', 'Failed') or exit_code != (0 if outcome == 'Passed' else 2):
        raise ValueError('Native exit and result disagree')
    if row.get('testName') != name or definition.get('name') != name:
        raise ValueError('Observation native identity was substituted')
    if not row.get('testId') or not row.get('executionId') or definition.get('id') != row.get('testId'):
        raise ValueError('Missing or inconsistent native test identity')
    execution = definition.findall('./{*}Execution')
    if (len(execution) != 1 or execution[0].get('id') != row.get('executionId') or
            entry.get('testId') != row.get('testId') or entry.get('executionId') != row.get('executionId')):
        raise ValueError('Native execution binding differs')
    methods = definition.findall('./{*}TestMethod')
    class_name, method = name.split('(provider:', 1)[0].rsplit('.', 1)
    if len(methods) != 1 or methods[0].get('className') != class_name or methods[0].get('name') != method:
        raise ValueError('Native method binding differs')
    properties = [(p.findtext('./{*}Key'), p.findtext('./{*}Value')) for p in definition.findall('./{*}Properties/{*}Property')]
    if sorted(properties) != [('BrowserStory', 'Accounts'), ('Category', 'Browser'), ('Provider', 'PostgreSql')]:
        raise ValueError('Native provider or story binding differs')
    required = {'total': '1', 'executed': '1', 'passed': str(int(outcome == 'Passed')), 'failed': str(int(outcome == 'Failed'))}
    zero = ('error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted', 'notRunnable',
            'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending')
    if any(counter.get(key) != value for key, value in required.items()) or any(counter.get(key) != '0' for key in zero):
        raise ValueError('Native counters are incomplete or contradictory')
    if summary.get('outcome') not in (('Completed', 'Passed') if outcome == 'Passed' else ('Failed',)):
        raise ValueError('Native whole-run outcome differs')
    errors = root.findall('.//{*}ErrorInfo')
    local_errors = row.findall('./{*}Output/{*}ErrorInfo')
    infos = root.findall('.//{*}RunInfo')
    known_exit = (len(infos) == 1 and infos[0].get('outcome') == 'Error' and
                  infos[0].findtext('./{*}Text') == NATIVE_EXIT_TWO)
    if ((not known_exit if outcome == 'Failed' else bool(infos)) or
            len(errors) != (1 if outcome == 'Failed' else 0) or errors != local_errors):
        raise ValueError('Unexpected run-level failure evidence')
    if outcome == 'Failed':
        message = local_errors[0].findtext('./{*}Message', '')
        if not re.match(r'^(?:Microsoft\.Playwright\.PlaywrightException : )?NS_ERROR_DOCUMENT_NOT_CACHED(?:\n| |$)', message):
            raise ValueError('Different native failure; no complete target observation')
    lines = [line for node in row.findall('./{*}Output/{*}StdOut') for line in (node.text or '').splitlines()]
    ledger = require_ledger(lines, outcome)
    return {'evidence_complete': True, 'native_outcome': outcome, 'native_exit': exit_code,
            'acceptance_credit': False, 'causal_closure': False, **ledger}
