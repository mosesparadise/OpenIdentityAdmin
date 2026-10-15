"""Diagnostic-only evidence validation; native failure is never acceptance."""
import json
import xml.etree.ElementTree as ET

ZERO = {'error','timeout','aborted','inconclusive','passedButRunAborted','notRunnable',
        'notExecuted','disconnected','warning','completed','inProgress','pending'}
EXIT = "Exit code indicates failure: '2'. Please refer to https://aka.ms/testingplatform/exitcodes for more information."
SOURCES = {'navigate-command','navigate-response','Network.requestWillBeSent','Network.loadingFailed',
           'Network.loadingFinished','Network.responseReceived','Page.frameDetached','Page.frameNavigated',
           'Page.didCheckNavigationPolicy','Playwright.provisionalLoadFailed','mapped-loading-failed-entry',
           'target-created','target-destroyed','target-committed','page-close-command','page-did-close'}
CATEGORIES = {'missing','empty','unknown','certificate','synthetic-coop-cancelled','policy-cancelled',
              'cancelled','canceled','connection-refused','timeout'}
BOOLS = {'mainFrame','exactUrl','document','nativeCancelled','status200','policyCancelled','exactPageProxy',
         'pageClosed','browserConnected','contextClosing','hasProvisional','currentSession','provisionalSession',
         'crashed','createdProvisional','loaderPresent','initialized','exactRequest','requestSession','exactLoader'}

def native(raw, selected, exit_code):
    assert len(raw) <= 2 * 1024 * 1024 and b'<!DOCTYPE' not in raw.upper() and b'<!ENTITY' not in raw.upper()
    root = ET.fromstring(raw)
    summaries = root.findall('./{*}ResultSummary')
    rows = root.findall('./{*}Results/{*}UnitTestResult')
    definitions = root.findall('./{*}TestDefinitions/{*}UnitTest')
    entries = root.findall('./{*}TestEntries/{*}TestEntry')
    assert len(summaries) == len(rows) == len(definitions) == len(entries) == 1
    counters = summaries[0].findall('./{*}Counters')
    assert len(counters) == 1 and set(counters[0].attrib) == ZERO | {'total','executed','passed','failed'}
    counts = {key: int(value) for key, value in counters[0].attrib.items()}
    assert all(counts[key] == 0 for key in ZERO)
    assert counts['total'] == counts['executed'] == 1
    passed = exit_code == 0
    assert exit_code in (0, 2) and counts['passed'] == int(passed) and counts['failed'] == int(not passed)
    row, definition, entry = rows[0], definitions[0], entries[0]
    expected_name = next(iter(selected.values()))[0]
    assert row.get('testName') == expected_name and row.get('outcome') == ('Passed' if passed else 'Failed')
    assert row.get('testId') and row.get('executionId')
    assert definition.get('name') == expected_name and definition.get('id') == row.get('testId')
    bindings = definition.findall('./{*}Execution')
    assert len(bindings) == 1 and bindings[0].get('id') == row.get('executionId')
    assert entry.get('testId') == row.get('testId') and entry.get('executionId') == row.get('executionId')
    infos, errors = root.findall('.//{*}RunInfo'), root.findall('.//{*}ErrorInfo')
    if passed:
        assert summaries[0].get('outcome') in ('Completed','Passed') and not infos and not errors
    else:
        assert summaries[0].get('outcome') == 'Failed' and len(errors) == 1
        assert errors == row.findall('./{*}Output/{*}ErrorInfo')
        assert errors[0].findtext('{*}Message') and errors[0].findtext('{*}StackTrace')
        assert len(infos) == 1 and infos[0].get('outcome') == 'Error' and infos[0].findtext('{*}Text') == EXIT
    return {'counts': counts, 'outcome': row.get('outcome'), 'test': expected_name, 'duration': row.get('duration')}

def observation(raw):
    assert len(raw) <= 128 * 1024
    report = json.loads(raw)
    assert type(report) is dict and type(report.get('phases')) is list
    assert set(report) == {'schema','limit','observed','dropped','instrumentationFailed','extraNavigations',
                           'navigationCount','scopeMatches','requestOverflow','retentionComplete','phases'}
    assert report['schema'] == 1 and report['limit'] == 128
    assert all(type(report[key]) is int and 0 <= report[key] <= 2**31-1
               for key in ('schema','limit','observed','dropped','extraNavigations','navigationCount'))
    assert all(type(report[key]) is bool for key in ('instrumentationFailed','scopeMatches','requestOverflow','retentionComplete'))
    assert report['navigationCount'] == len(report['phases']) <= 3
    rows = []
    for index, phase in enumerate(report['phases']):
        assert type(phase) is dict and type(phase.get('events')) is list
        assert set(phase) == {'navigation','mainFrame','matchingRequests','requestBindings','ambiguousRequestIdentity','loaderPresent','events'}
        assert all(type(phase[key]) is int and 0 <= phase[key] <= 2**31-1 for key in ('navigation','matchingRequests','requestBindings'))
        assert phase['navigation'] == index + 1 and 0 <= phase['requestBindings'] <= 4
        assert phase['matchingRequests'] >= phase['requestBindings']
        assert all(type(phase[key]) is bool for key in ('mainFrame','loaderPresent','ambiguousRequestIdentity'))
        for row in phase['events']:
            assert type(row) is dict
            assert set(row) <= BOOLS | {'ordinal','elapsedMs','source','category','binding'}
            assert row['source'] in SOURCES
            assert type(row['ordinal']) is int and 1 <= row['ordinal'] <= 128
            assert type(row['elapsedMs']) is int and row['elapsedMs'] >= 0
            assert all(row[key] is None or type(row[key]) is bool for key in row.keys() & BOOLS)
            if 'category' in row: assert row['category'] in CATEGORIES
            if 'binding' in row: assert row['binding'] is None or type(row['binding']) is int and 1 <= row['binding'] <= phase['requestBindings']
            rows.append(row)
    assert len(rows) <= 128 and len({row['ordinal'] for row in rows}) == len(rows)
    assert sorted(row['ordinal'] for row in rows) == list(range(1, len(rows) + 1))
    assert report['observed'] == len(rows) + report['dropped']
    retention = report['retentionComplete'] is True and report['instrumentationFailed'] is False and report['requestOverflow'] is False
    retention &= report['dropped'] == report['extraNavigations'] == 0 and not any(p['ambiguousRequestIdentity'] for p in report['phases'])
    scope = report['scopeMatches'] is True and report['navigationCount'] == 3
    scope &= all(p['mainFrame'] is True and p['loaderPresent'] is True for p in report['phases'])
    def correlated(row):
        return row.get('exactRequest') is True and row.get('requestSession') is True and row.get('exactLoader') is True and row.get('binding') is not None
    def positive(phase):
        return any(row['source'] == 'Network.responseReceived' and row.get('status200') is True and correlated(row) for row in phase['events'])
    positives = len(report['phases']) >= 2 and all(positive(p) for p in report['phases'][:2])
    third = report['phases'][2]['events'] if len(report['phases']) == 3 else []
    failures = [row for row in third if row['source'] == 'Network.loadingFailed' and correlated(row) or
                row['source'] == 'Playwright.provisionalLoadFailed' and row.get('exactPageProxy') is True and row.get('exactLoader') is True]
    unexpected_success = any(row['source'] == 'Network.responseReceived' and row.get('status200') is True and correlated(row) for row in third)
    return {'retention_complete': bool(retention), 'scope_matches': bool(scope), 'trusted_positives': bool(positives),
            'correlated_negative_failures': failures, 'unexpected_untrusted_http200': unexpected_success,
            'diagnostic_complete': bool(retention and scope and positives and failures and not unexpected_success),
            'acceptance': False}
