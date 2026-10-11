"""Fail closed when a selected issuer partition drops, duplicates or substitutes a test."""
import json
import xml.etree.ElementTree as ET


def catalogue(raw):
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
        if browser != (test['type']['namespace'] == 'IdentityPlatform.Issuer.Tests.Features.Browser'):
            raise ValueError('Browser namespace and trait disagree')
        result[uid] = (name, providers[0], browser)
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
