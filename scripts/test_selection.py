"""Fail closed when a selected issuer partition drops, duplicates or substitutes a test."""
import json
import xml.etree.ElementTree as ET


def select_projects(matrix, profile, browser=None, partition='all'):
    """Select reviewed project partitions while rejecting incompatible unit or browser requests."""
    if partition not in ('all', 'standard', 'foundation', 'issuer', 'lifecycle', 'administrator'):
        raise ValueError('Unknown test partition')
    if partition != 'all' and (profile == 'unit' or (browser and partition in ('foundation', 'issuer', 'lifecycle'))):
        raise ValueError('Partition is incompatible with this profile or browser')
    projects = matrix['browser'] if browser else matrix['profiles'][profile]
    if partition == 'foundation':
        return {name: projects[name] for name in ('Persistence', 'AppHost')}
    if partition == 'issuer':
        return {'Issuer': projects['Issuer']}
    if partition == 'standard':
        return {name: count for name, count in projects.items() if name not in ('Issuer.Lifecycle', 'Administrator')}
    if partition == 'administrator' and 'Administrator' not in projects:
        raise ValueError('Administrator partition is absent from the reviewed matrix')
    if partition == 'administrator':
        return {'Administrator': projects['Administrator']}
    if partition == 'lifecycle':
        return {'Issuer.Lifecycle': projects['Issuer.Lifecycle']}
    return dict(projects)


def catalogue(raw, lifecycle=False, project=None):
    project = project or ('Issuer.Lifecycle' if lifecycle else 'Issuer')
    if project not in ('Issuer', 'Issuer.Lifecycle', 'Administrator', 'AppHost', 'Persistence'):
        raise ValueError('Unknown discovery project')
    value = json.loads(raw)
    if value.get('schemaVersion') != 1 or not isinstance(value.get('tests'), list):
        raise ValueError('Unsupported test discovery document')
    result = {}
    names = set()
    for test in value['tests']:
        uid, name = test['uid'], test['displayName']
        if not uid or not name or uid in result or name in names:
            raise ValueError('Duplicate or missing test identity')
        traits = test['traits']
        providers = [x['value'] for x in traits if x['key'] == 'Provider']
        if len(providers) != 1 or providers[0] not in ('PostgreSql', 'SqlServer'):
            raise ValueError('Issuer test lacks one recognized provider')
        browser = any(x == {'key': 'Category', 'value': 'Browser'} for x in traits)
        if project in ('Issuer.Lifecycle', 'Persistence') and (browser or '.Features.Browser' in test['type']['namespace']):
            raise ValueError('Lifecycle catalogue contains a browser namespace or trait')
        if browser != (test['type']['namespace'] == 'IdentityPlatform.' + project + '.Tests.Features.Browser'):
            raise ValueError('Browser namespace and trait disagree')
        result[uid] = (name, providers[0], browser, test['type']['namespace']) if project != 'Issuer' else (name, providers[0], browser)
        if project == 'Administrator':
            stories = [trait['value'] for trait in traits if trait['key'] == 'BrowserStory']
            if (browser and (len(stories) != 1 or stories[0] not in ('Accounts', 'Security'))) or (not browser and stories):
                raise ValueError('Administrator browser story trait is missing, ambiguous or inappropriate')
            result[uid] = (*result[uid], stories[0] if browser else None)
        names.add(name)
    if not result:
        raise ValueError('Empty discovery document')
    return result


def require_partition(all_tests, http, browser, provider, http_count, browser_count):
    expected = {uid: test for uid, test in all_tests.items() if test[1] == provider}
    if set(http) & set(browser) or http | browser != expected:
        raise ValueError('HTTP/browser partition overlaps, omits or substitutes tests')
    if len(http) != http_count or len(browser) != browser_count:
        raise ValueError('Issuer partition count changed without review')
    if any(test[2] for test in http.values()) or any(not test[2] for test in browser.values()):
        raise ValueError('Issuer partition contains the wrong category')


def require_execution_names(path, selected):
    results = ET.parse(path).getroot().findall('.//{*}UnitTestResult')
    names = [value.attrib['testName'] for value in results]
    if len(names) != len(set(names)) or set(names) != {test[0] for test in selected.values()}:
        raise ValueError('Executed tests do not match discovered selection')


def require_lifecycle_partition(all_tests, selected, provider, counts):
    if set(counts) != {'PostgreSql', 'SqlServer'} or any(value <= 0 for value in counts.values()):
        raise ValueError('Lifecycle requires both provider counts')
    if any(test[2] for test in all_tests.values()):
        raise ValueError('Lifecycle catalogue contains browser cases')
    for name, count in counts.items():
        if sum(test[1] == name for test in all_tests.values()) != count:
            raise ValueError('Lifecycle provider catalogue is incomplete')
    if selected != {uid: test for uid, test in all_tests.items() if test[1] == provider}:
        raise ValueError('Lifecycle selection omits or substitutes provider cases')


def require_lifecycle_execution(path, selected):
    root = ET.parse(path).getroot()
    summaries = root.findall('./{*}ResultSummary')
    if len(summaries) != 1 or summaries[0].get('outcome') not in ('Completed', 'Passed'):
        raise ValueError('Lifecycle run did not complete successfully')
    if root.findall('.//{*}ErrorInfo') or any(info.get('outcome') not in ('Passed', 'Completed') for info in root.findall('.//{*}RunInfo')):
        raise ValueError('Lifecycle report contains run errors')
    counters = root.findall('./{*}ResultSummary/{*}Counters')
    required_zero = {'failed', 'error', 'timeout', 'aborted', 'inconclusive', 'passedButRunAborted',
                     'notRunnable', 'notExecuted', 'disconnected', 'warning', 'completed', 'inProgress', 'pending'}
    if len(counters) != 1 or any(counters[0].get(key) != '0' for key in required_zero):
        raise ValueError('Lifecycle counters are incomplete or contradictory')
    if any(counters[0].get(key) != str(len(selected)) for key in ('total', 'executed', 'passed')):
        raise ValueError('Lifecycle counters do not match the selected results')
    rows = root.findall('./{*}Results/{*}UnitTestResult')
    definitions = root.findall('./{*}TestDefinitions/{*}UnitTest')
    entries = root.findall('./{*}TestEntries/{*}TestEntry')
    expected = {test[0] for test in selected.values()}
    if not expected or len(rows) != len(expected) or any(row.get('outcome') != 'Passed' for row in rows):
        raise ValueError('Lifecycle result rows are incomplete or nonpassing')
    names = [row.get('testName') for row in rows]
    ids = [row.get('testId') for row in rows]
    executions = [row.get('executionId') for row in rows]
    if set(names) != expected or any(not value for value in ids + executions) or len(set(ids)) != len(rows) or len(set(executions)) != len(rows):
        raise ValueError('Lifecycle result identities are inconsistent')
    if len(definitions) != len(rows) or {d.get('id'): d.get('name') for d in definitions} != dict(zip(ids, names)):
        raise ValueError('Lifecycle definitions do not match results')
    bindings = {(row.get('testId'), row.get('executionId')) for row in rows}
    if len(entries) != len(rows) or {(e.get('testId'), e.get('executionId')) for e in entries} != bindings:
        raise ValueError('Lifecycle entries do not match results')
    for definition in definitions:
        executions = definition.findall('./{*}Execution')
        if len(executions) != 1 or (definition.get('id'), executions[0].get('id')) not in bindings:
            raise ValueError('Lifecycle definition execution is inconsistent')


def lifecycle_invocations(matrix, profile, story=None):
    stories = matrix['lifecycle_stories']
    if set(stories) != {'Restart', 'Distribution', 'KeyRotation.Activation', 'KeyRotation.Retirement'}:
        raise ValueError('Lifecycle stories must be explicitly reviewed')
    if story is not None and story not in stories:
        raise ValueError('Unknown lifecycle story')
    for provider in ('PostgreSql', 'SqlServer'):
        if any(set(counts) != {'PostgreSql', 'SqlServer'} or any(n <= 0 for n in counts.values()) for counts in stories.values()):
            raise ValueError('Each lifecycle story requires both providers')
        if sum(counts[provider] for counts in stories.values()) != matrix['profiles'][provider]['Issuer.Lifecycle']:
            raise ValueError('Lifecycle stories do not exhaust the provider profile')
    return [(name, stories[name][profile]) for name in stories if story is None or name == story]


def require_lifecycle_story(all_tests, selected, provider, story, matrix):
    lifecycle_invocations(matrix, provider, story)
    namespaces = {name: 'IdentityPlatform.Issuer.Lifecycle.Tests.Features.' + name for name in matrix['lifecycle_stories']}
    if any(len(test) != 4 or test[3] not in namespaces.values() or test[2] for test in all_tests.values()):
        raise ValueError('Lifecycle case has an unreviewed story namespace')
    complete = {uid: test for uid, test in all_tests.items() if test[1] == provider}
    require_lifecycle_partition(all_tests, complete, provider,
        {p: matrix['profiles'][p]['Issuer.Lifecycle'] for p in ('PostgreSql', 'SqlServer')})
    for name, namespace in namespaces.items():
        for p, count in matrix['lifecycle_stories'][name].items():
            if sum(test[1] == p and test[3] == namespace for test in all_tests.values()) != count:
                raise ValueError('Lifecycle story catalogue count changed without review')
    expected = {uid: test for uid, test in complete.items() if test[3] == namespaces[story]}
    if selected != expected:
        raise ValueError('Lifecycle story selection overlaps, omits or substitutes identities')


def require_administrator_partition(all_tests, selected, provider, counts, expected_names, browser_count=0):
    if provider not in ('PostgreSql', 'SqlServer') or set(counts) != {'PostgreSql', 'SqlServer'} or set(expected_names) != set(counts):
        raise ValueError('Administrator requires both provider inventories')
    for name, count in counts.items():
        expected = expected_names[name]
        actual = [test[0] for test in all_tests.values() if test[1] == name]
        if count <= 0 or len(expected) != count + browser_count or len(set(expected)) != count + browser_count or sorted(actual) != sorted(expected):
            raise ValueError('Administrator catalogue differs from reviewed identities')
    ordinary = {'IdentityPlatform.Administrator.Tests.Features.' + story for story in ('Bootstrap', 'Factors', 'Ceremonies', 'Accounts', 'Recovery', 'Security', 'Governance.Policy', 'Governance.Commands', 'Governance.Failures', 'ProtocolAdmission', 'BrowserSessions')}
    browser_namespace = 'IdentityPlatform.Administrator.Tests.Features.Browser'
    if any(len(test) != 5 or (test[3] != browser_namespace if test[2] else test[3] not in ordinary) or
           (test[4] not in ('Accounts', 'Security') if test[2] else test[4] is not None) for test in all_tests.values()):
        raise ValueError('Administrator catalogue contains an unexpected namespace or category')
    if type(browser_count) is not int or browser_count < 0 or any(
            sum(test[1] == p and test[2] for test in all_tests.values()) != browser_count for p in counts):
        raise ValueError('Administrator browser inventory is incomplete')
    if selected != {uid: test for uid, test in all_tests.items() if test[1] == provider}:
        raise ValueError('Administrator selection omits or substitutes provider cases')


def administrator_invocations(matrix, profile, story=None):
    stories = matrix['administrator_stories']
    if profile not in ('PostgreSql', 'SqlServer') or set(stories) != {'Bootstrap', 'Factors', 'Ceremonies', 'Accounts', 'Recovery', 'Security', 'Governance.Policy', 'Governance.Commands', 'Governance.Failures', 'ProtocolAdmission', 'BrowserSessions'}:
        raise ValueError('Administrator stories and provider must be explicitly reviewed')
    if story is not None and story not in stories:
        raise ValueError('Unknown administrator story')
    for provider in ('PostgreSql', 'SqlServer'):
        if any(set(counts) != {'PostgreSql', 'SqlServer'} or any(type(n) is not int or n <= 0 for n in counts.values()) for counts in stories.values()):
            raise ValueError('Each administrator story requires both providers')
        if sum(counts[provider] for counts in stories.values()) != matrix['profiles'][provider]['Administrator']:
            raise ValueError('Administrator stories do not exhaust the provider profile')
    return [(name, stories[name][profile]) for name in stories if story is None or name == story]


def administrator_catalogue(all_tests, provider, matrix, inventory):
    """Validate every ordinary and browser identity, including each story's independent counts."""
    administrator_invocations(matrix, provider)
    complete = {uid: test for uid, test in all_tests.items() if test[1] == provider}
    require_administrator_partition(all_tests, complete, provider,
        {p: matrix['profiles'][p]['Administrator'] for p in ('PostgreSql', 'SqlServer')}, inventory, matrix.get('browser', {}).get('Administrator', 0))
    namespaces = {name: 'IdentityPlatform.Administrator.Tests.Features.' + name for name in matrix['administrator_stories']}
    for name, namespace in namespaces.items():
        for p, count in matrix['administrator_stories'][name].items():
            if sum(test[1] == p and test[3] == namespace for test in all_tests.values()) != count:
                raise ValueError('Administrator story catalogue count changed without review')
    if matrix.get('browser', {}).get('Administrator', 0):
        for current, count in administrator_browser_invocations(matrix):
            for p in ('PostgreSql', 'SqlServer'):
                if sum(test[1] == p and test[2] and test[4] == current for test in all_tests.values()) != count:
                    raise ValueError('Administrator browser story count changed without review')
    return complete, namespaces


def require_administrator_story(all_tests, selected, provider, story, matrix, inventory):
    administrator_invocations(matrix, provider, story)
    complete, namespaces = administrator_catalogue(all_tests, provider, matrix, inventory)
    expected = {uid: test for uid, test in complete.items() if test[3] == namespaces[story]}
    if selected != expected:
        raise ValueError('Administrator story selection overlaps, omits or substitutes identities')


def require_administrator_browser(all_tests, selected, provider, matrix, inventory, story=None):
    """Require the complete reviewed inventory and an exact disjoint browser selection for this provider."""
    browser_count = matrix['browser']['Administrator']
    if type(browser_count) is not int or browser_count <= 0:
        raise ValueError('Administrator browser coverage must be positive')
    complete, _ = administrator_catalogue(all_tests, provider, matrix, inventory)
    administrator_browser_invocations(matrix, story)
    expected = {uid: test for uid, test in complete.items() if test[2] and (story is None or test[4] == story)}
    if selected != expected:
        raise ValueError('Administrator browser selection omits or substitutes identities')


def administrator_browser_invocations(matrix, story=None):
    """Keep both reviewed browser stories exhaustive and independently bounded by the unchanged invocation deadline."""
    stories = matrix.get('administrator_browser_stories', {})
    if set(stories) != {'Accounts', 'Security'} or any(type(count) is not int or count <= 0 for count in stories.values()):
        raise ValueError('Administrator browser stories must be explicitly reviewed')
    if sum(stories.values()) != matrix['browser']['Administrator'] or (story is not None and story not in stories):
        raise ValueError('Administrator browser stories do not exhaust their reviewed inventory')
    return [(name, count) for name, count in stories.items() if story is None or name == story]
