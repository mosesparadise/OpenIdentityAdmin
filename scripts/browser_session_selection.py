"""Bound ordinary browser-session execution without changing any native test identity."""
import re
from test_selection import administrator_invocations, require_administrator_story

PROVIDERS = ('PostgreSql', 'SqlServer')
GROUPS = ('Foundations', 'Product', 'Lifecycle')
NAMESPACE = 'IdentityPlatform.Administrator.Tests.Features.BrowserSessions'


def administrator_execution_invocations(matrix, provider, story=None, group=None):
    """Expand default and explicit browser-session stories into every required execution group."""
    if group is not None and story != 'BrowserSessions':
        raise ValueError('Browser-session group cannot select another story')
    result = []
    for current, count in administrator_invocations(matrix, provider, story):
        groups = browser_session_invocations(matrix, provider, group) if current == 'BrowserSessions' else [(None, count)]
        result.extend((current, selected, expected) for selected, expected in groups)
    return result


def browser_session_invocations(matrix, provider, group=None):
    """Require explicit nonoverlapping classes and counts exhausting the unchanged story."""
    groups = matrix['browser_session_groups']
    if provider not in PROVIDERS or set(groups) != set(GROUPS) or (group is not None and group not in GROUPS):
        raise ValueError('Unknown browser-session provider or execution group')
    classes = []
    for item in groups.values():
        if set(item) != {'classes', 'counts'} or not isinstance(item['classes'], list) or not item['classes']:
            raise ValueError('Browser-session groups require explicit classes and counts')
        if any(not isinstance(name, str) or not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*', name) for name in item['classes']):
            raise ValueError('Invalid browser-session class name')
        if set(item['counts']) != set(PROVIDERS) or any(type(n) is not int or n <= 0 for n in item['counts'].values()):
            raise ValueError('Browser-session groups require both positive provider counts')
        classes.extend(item['classes'])
    if len(classes) != len(set(classes)):
        raise ValueError('Browser-session classes overlap')
    for current in PROVIDERS:
        if sum(item['counts'][current] for item in groups.values()) != matrix['administrator_stories']['BrowserSessions'][current]:
            raise ValueError('Browser-session groups do not exhaust the story')
    return [(name, groups[name]['counts'][provider]) for name in GROUPS if group is None or name == group]


def browser_session_filter(matrix, provider, group):
    """Use positive full-class filters built only from reviewed identifiers."""
    browser_session_invocations(matrix, provider, group)
    types = '|'.join('FullyQualifiedName~' + NAMESPACE + '.' + name + '.'
                     for name in sorted(matrix['browser_session_groups'][group]['classes']))
    return f'Provider={provider}&Category!=Browser&({types})'


def require_browser_session_selections(all_tests, selections, provider, matrix, inventory):
    """Validate the full native catalogue and every actual group filter before any executes."""
    browser_session_invocations(matrix, provider)
    if set(selections) != set(GROUPS):
        raise ValueError('All browser-session group selections are required')
    names = {current: inventory[current] for current in PROVIDERS}
    expected = {uid: test for uid, test in all_tests.items() if test[1] == provider and test[3] == NAMESPACE}
    require_administrator_story(all_tests, expected, provider, 'BrowserSessions', matrix, names)
    classes = {group: set(matrix['browser_session_groups'][group]['classes']) for group in GROUPS}
    def kind(test):
        metadata = inventory['metadata'][test[0]]['type']
        if metadata['namespace'] != NAMESPACE or not test[0].startswith(NAMESPACE + '.' + metadata['typeName'] + '.'):
            raise ValueError('Browser-session identity disagrees with reviewed type metadata')
        return metadata['typeName']
    for current in PROVIDERS:
        story = [test for test in all_tests.values() if test[1] == current and test[3] == NAMESPACE]
        if {kind(test) for test in story} != set().union(*classes.values()):
            raise ValueError('Browser-session classes are missing or unreviewed')
        for group, count in browser_session_invocations(matrix, current):
            if sum(kind(test) in classes[group] for test in story) != count:
                raise ValueError('Browser-session group discovery count changed')
    combined = {}
    for group in GROUPS:
        selected = {uid: test for uid, test in expected.items() if kind(test) in classes[group]}
        if selections[group] != selected or combined.keys() & selections[group].keys():
            raise ValueError('Browser-session group omits, overlaps or substitutes native identities')
        combined.update(selections[group])
    if combined != expected:
        raise ValueError('Browser-session groups do not exhaust actual native discovery')
