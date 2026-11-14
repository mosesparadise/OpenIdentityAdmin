"""Bind provider-independent sample state witnesses without relaxing existing native database inventories."""
import json
import re

NAMESPACE = 'IdentityPlatform.Host.Tests.Features.Samples'
DECISIONS = frozenset(('sample-state-cas', 'sample-ticket-removal', 'sample-publication-gate'))


def sample_filter(decision):
    """Permit only reviewed state/ticket decisions as exact native trait selections."""
    if decision not in DECISIONS:
        raise ValueError('Unknown sample decision')
    return 'CriticalDecision=' + decision


def sample_catalogue(raw):
    """Require unique unit identities and one reviewed decision, never a browser/provider or substituted namespace."""
    document = json.loads(raw)
    if document.get('schemaVersion') != 1 or not isinstance(document.get('tests'), list):
        raise ValueError('Unsupported sample discovery document')
    result, names = {}, set()
    for test in document['tests']:
        uid, name, kind = test['uid'], test['displayName'], test['type']
        traits = test['traits']
        decisions = [trait['value'] for trait in traits if trait['key'] == 'CriticalDecision']
        if (not uid or uid in result or name in names or kind['namespace'] != NAMESPACE or
                not re.fullmatch(r'[A-Za-z][A-Za-z0-9]*', kind['typeName']) or
                not name.startswith(NAMESPACE + '.' + kind['typeName'] + '.') or
                len(decisions) != 1 or decisions[0] not in DECISIONS or
                any(trait['key'] in ('Provider', 'Category', 'BrowserStory') for trait in traits)):
            raise ValueError('Unreviewed sample mutation identity')
        result[uid] = (name, None, False, NAMESPACE, decisions[0])
        names.add(name)
    if not result:
        raise ValueError('Empty sample discovery')
    return result


def sample_identity(selection, item):
    """A reviewed Fact has one exact native name; matching a method prefix is insufficient."""
    return len(selection) == 1 and next(iter(selection.values())) == (item['method'], None, False, NAMESPACE, item['id'])
