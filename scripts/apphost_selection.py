"""Keep native AppHost execution bounded while requiring both complete provider groups."""
import json
import re
from test_selection import catalogue

PROVIDERS = ('PostgreSql', 'SqlServer')
NAMESPACES = {
    'Foundation': 'IdentityPlatform.AppHost.Tests.Features',
    'BffSessions': 'IdentityPlatform.AppHost.Tests.Features.BffSessions',
}


def apphost_invocations(matrix, provider):
    """Require both positive groups to exhaust each unchanged AppHost provider total."""
    groups = matrix['apphost_native_groups']
    if provider not in PROVIDERS or set(groups) != set(NAMESPACES):
        raise ValueError('Unknown native AppHost provider or groups')
    for counts in groups.values():
        if set(counts) != set(PROVIDERS) or any(type(n) is not int or n <= 0 for n in counts.values()):
            raise ValueError('Each AppHost group requires positive counts for both providers')
    for name in PROVIDERS:
        if sum(counts[name] for counts in groups.values()) != matrix['profiles'][name]['AppHost']:
            raise ValueError('Native AppHost groups do not exhaust the provider total')
    return [(group, groups[group][provider]) for group in NAMESPACES]


def apphost_catalogue(raw):
    """Bind native provider discovery to its exact display identity and reviewed namespace."""
    tests = catalogue(raw, project='AppHost')
    metadata = {test['uid']: test['type'] for test in json.loads(raw)['tests']}
    for uid, test in tests.items():
        kind = metadata[uid]
        if test[2] or test[3] not in NAMESPACES.values() or not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*', kind.get('typeName', '')):
            raise ValueError('Native AppHost discovery has unreviewed type metadata')
        if not test[0].startswith(test[3] + '.' + kind['typeName'] + '.'):
            raise ValueError('AppHost display identity disagrees with discovered type')
    return tests


def apphost_filter(provider, group):
    """Select reviewed namespace ownership without offering an optional group execution path."""
    if provider not in PROVIDERS or group not in NAMESPACES:
        raise ValueError('Unknown native AppHost filter')
    prefix = f'Provider={provider}&FullyQualifiedName~{NAMESPACES[group]}.'
    return prefix if group == 'BffSessions' else prefix + f'&FullyQualifiedName!~{NAMESPACES["BffSessions"]}.'


def require_apphost_selections(all_tests, selections, provider, matrix):
    """Require exact disjoint identities, metadata and counts before either invocation runs."""
    groups = apphost_invocations(matrix, provider)
    if set(selections) != set(NAMESPACES) or any(test[1] != provider for test in all_tests.values()):
        raise ValueError('Native AppHost discovery or selections have the wrong provider/groups')
    combined = {}
    for group, count in groups:
        expected = {uid: test for uid, test in all_tests.items() if test[3] == NAMESPACES[group]}
        if len(expected) != count or selections[group] != expected or combined.keys() & selections[group].keys():
            raise ValueError('Native AppHost group omits, overlaps or substitutes required identities')
        combined.update(selections[group])
    if combined != all_tests or len(combined) != matrix['profiles'][provider]['AppHost']:
        raise ValueError('Native AppHost groups do not exhaust discovery')
