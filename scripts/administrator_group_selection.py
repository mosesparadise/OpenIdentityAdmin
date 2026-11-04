"""Partition reviewed administrator stories without dropping or weakening native evidence."""
import re
from test_selection import administrator_invocations, require_administrator_story

PROVIDERS = ('PostgreSql', 'SqlServer')
DEFINITIONS = {
    'BrowserSessions': ('browser_session_groups', ('Foundations', 'Product', 'Lifecycle')),
    'ManagedApplications': ('managed_application_groups', ('Admission', 'Issuance')),
}
NAMESPACE = 'IdentityPlatform.Administrator.Tests.Features.'


def definition(story):
    """Only explicitly reviewed stories may acquire execution groups."""
    if story not in DEFINITIONS:
        raise ValueError('Unknown grouped administrator story')
    return DEFINITIONS[story]


def administrator_execution_invocations(matrix, provider, story=None, group=None, managed_group=None):
    """Default and explicit story runs include every required nonoverlapping group."""
    if group is not None and story != 'BrowserSessions':
        raise ValueError('Browser-session group cannot select another story')
    if managed_group is not None and story != 'ManagedApplications':
        raise ValueError('Managed-application group cannot select another story')
    result = []
    for current, count in administrator_invocations(matrix, provider, story):
        requested = group if current == 'BrowserSessions' else managed_group
        groups = group_invocations(matrix, provider, current, requested) if current in DEFINITIONS else [(None, count)]
        result.extend((current, selected, expected) for selected, expected in groups)
    return result


def group_invocations(matrix, provider, story, group=None):
    """Require explicit classes, both-provider counts and a complete story partition."""
    key, names = definition(story)
    groups = matrix[key]
    if provider not in PROVIDERS or set(groups) != set(names) or (group is not None and group not in names):
        raise ValueError('Unknown administrator provider or execution group')
    classes = []
    for item in groups.values():
        if set(item) != {'classes', 'counts'} or not isinstance(item['classes'], list) or not item['classes']:
            raise ValueError('Administrator groups require explicit classes and counts')
        if any(not isinstance(name, str) or not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*', name) for name in item['classes']):
            raise ValueError('Invalid administrator class name')
        if set(item['counts']) != set(PROVIDERS) or any(type(n) is not int or n <= 0 for n in item['counts'].values()):
            raise ValueError('Administrator groups require both positive provider counts')
        classes.extend(item['classes'])
    if len(classes) != len(set(classes)):
        raise ValueError('Administrator classes overlap')
    for current in PROVIDERS:
        if sum(item['counts'][current] for item in groups.values()) != matrix['administrator_stories'][story][current]:
            raise ValueError('Administrator groups do not exhaust the story')
    return [(name, groups[name]['counts'][provider]) for name in names if group is None or name == group]


def group_filter(matrix, provider, story, group):
    """Build only positive full-class native filters from reviewed identifiers."""
    group_invocations(matrix, provider, story, group)
    key, _ = definition(story)
    types = '|'.join('FullyQualifiedName~' + NAMESPACE + story + '.' + name + '.'
                     for name in sorted(matrix[key][group]['classes']))
    return f'Provider={provider}&Category!=Browser&({types})'


def require_group_selections(all_tests, selections, provider, story, matrix, inventory):
    """Check every group's actual native tuples, including both-provider metadata and the exact disjoint union."""
    group_invocations(matrix, provider, story)
    key, names = definition(story)
    namespace = NAMESPACE + story
    if set(selections) != set(names):
        raise ValueError('All administrator group selections are required')
    expected = {uid: test for uid, test in all_tests.items() if test[1] == provider and test[3] == namespace}
    require_administrator_story(all_tests, expected, provider, story, matrix, {current: inventory[current] for current in PROVIDERS})
    classes = {group: set(matrix[key][group]['classes']) for group in names}

    def kind(test):
        metadata = inventory['metadata'][test[0]]['type']
        if metadata['namespace'] != namespace or not test[0].startswith(namespace + '.' + metadata['typeName'] + '.'):
            raise ValueError('Administrator identity disagrees with reviewed type metadata')
        return metadata['typeName']

    for current in PROVIDERS:
        tests = [test for test in all_tests.values() if test[1] == current and test[3] == namespace]
        if {kind(test) for test in tests} != set().union(*classes.values()):
            raise ValueError('Administrator classes are missing or unreviewed')
        for group, count in group_invocations(matrix, current, story):
            if sum(kind(test) in classes[group] for test in tests) != count:
                raise ValueError('Administrator group discovery count changed')
    combined = {}
    for group in names:
        selected = {uid: test for uid, test in expected.items() if kind(test) in classes[group]}
        if selections[group] != selected or combined.keys() & selections[group].keys():
            raise ValueError('Administrator group omits, overlaps or substitutes native identities')
        combined.update(selections[group])
    if combined != expected:
        raise ValueError('Administrator groups do not exhaust actual native discovery')
