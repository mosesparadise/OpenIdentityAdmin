"""Keep fixed issuer HTTP groups exhaustive without splitting shared fixture collections."""
import json
import re
from test_selection import catalogue

PROVIDERS = ('PostgreSql', 'SqlServer')
GROUPS = ('Flows', 'Boundaries')
PROTOCOL_NAMESPACE = 'IdentityPlatform.Issuer.Tests.Features.Protocol'
BROWSER_NAMESPACE = 'IdentityPlatform.Issuer.Tests.Features.Browser'


def issuer_catalogue(raw):
    """Bind every native UID/name/provider/category to its actual discovered class metadata."""
    tests = catalogue(raw)
    metadata = {test['uid']: test['type'] for test in json.loads(raw)['tests']}
    result = {}
    for uid, test in tests.items():
        kind = metadata[uid]
        expected_namespace = BROWSER_NAMESPACE if test[2] else PROTOCOL_NAMESPACE
        if kind['namespace'] != expected_namespace or not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*', kind.get('typeName', '')):
            raise ValueError('Issuer discovery has unreviewed type metadata')
        full_type = expected_namespace + '.' + kind['typeName']
        if not test[0].startswith(full_type + '.'):
            raise ValueError('Issuer display identity disagrees with its discovered type')
        result[uid] = (*test, full_type)
    return result


def issuer_invocations(matrix, provider, group=None):
    """Require fixed groups, unique explicit classes and positive counts exhausting both provider totals."""
    groups = matrix['issuer_http_groups']
    if provider not in PROVIDERS or set(groups) != set(GROUPS) or (group is not None and group not in GROUPS):
        raise ValueError('Unknown issuer provider or execution group')
    classes = []
    for item in groups.values():
        if set(item) != {'classes', 'counts'} or not isinstance(item['classes'], list) or not item['classes']:
            raise ValueError('Issuer group requires explicit classes and counts')
        if any(not isinstance(name, str) or not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*', name) for name in item['classes']):
            raise ValueError('Issuer group class names are invalid')
        counts = item['counts']
        if set(counts) != set(PROVIDERS) or any(type(n) is not int or n <= 0 for n in counts.values()):
            raise ValueError('Issuer groups require both positive provider counts')
        classes.extend(item['classes'])
    if len(classes) != len(set(classes)):
        raise ValueError('Issuer classes overlap within or across groups')
    for name in PROVIDERS:
        if sum(item['counts'][name] for item in groups.values()) != matrix['profiles'][name]['Issuer']:
            raise ValueError('Issuer groups do not exhaust the provider total')
    return [(name, groups[name]['counts'][provider]) for name in GROUPS if group is None or name == group]


def issuer_types(matrix, group):
    """Return reviewed fully qualified classes after the caller validates the manifest."""
    return {PROTOCOL_NAMESPACE + '.' + name for name in matrix['issuer_http_groups'][group]['classes']}


def require_issuer_inventory(all_tests, matrix):
    """Require every discovered HTTP type exactly once and all reviewed provider/browser counts."""
    issuer_invocations(matrix, PROVIDERS[0])
    expected_types = set().union(*(issuer_types(matrix, group) for group in GROUPS))
    if any(len(test) != 4 or test[1] not in PROVIDERS for test in all_tests.values()):
        raise ValueError('Issuer inventory metadata is incomplete')
    for provider in PROVIDERS:
        http = {uid: test for uid, test in all_tests.items() if test[1] == provider and not test[2]}
        if {test[3] for test in http.values()} != expected_types or len(http) != matrix['profiles'][provider]['Issuer']:
            raise ValueError('Issuer HTTP catalogue contains missing or unreviewed classes')
        if sum(test[1] == provider and test[2] for test in all_tests.values()) != matrix['browser']['Issuer']:
            raise ValueError('Issuer browser inventory is incomplete')
        for group, count in issuer_invocations(matrix, provider):
            if sum(test[3] in issuer_types(matrix, group) for test in http.values()) != count:
                raise ValueError('Issuer group discovery count differs from review')


def require_issuer_group(all_tests, selected, provider, group, matrix):
    """Reject any missing, substituted, duplicate, browser or wrong-provider selected identity."""
    issuer_invocations(matrix, provider, group)
    require_issuer_inventory(all_tests, matrix)
    expected = {uid: test for uid, test in all_tests.items()
                if test[1] == provider and not test[2] and test[3] in issuer_types(matrix, group)}
    if selected != expected:
        raise ValueError('Issuer group selection omits or substitutes native identities')


def issuer_filter(matrix, provider, group):
    """Build a bounded native filter from validated manifest identifiers, never arbitrary expressions."""
    issuer_invocations(matrix, provider, group)
    types = '|'.join('FullyQualifiedName~' + name + '.' for name in sorted(issuer_types(matrix, group)))
    return f'Provider={provider}&Category!=Browser&({types})'


def require_issuer_selections(all_tests, selections, provider, matrix):
    """Prove both native filters form one exact disjoint HTTP catalogue before executing either group."""
    if set(selections) != set(GROUPS):
        raise ValueError('Both issuer group selections are required')
    combined = {}
    for group in GROUPS:
        require_issuer_group(all_tests, selections[group], provider, group, matrix)
        if combined.keys() & selections[group].keys():
            raise ValueError('Native issuer group filters overlap')
        combined.update(selections[group])
    expected = {uid: test for uid, test in all_tests.items() if test[1] == provider and not test[2]}
    if combined != expected:
        raise ValueError('Native issuer filters do not exhaust HTTP discovery')
