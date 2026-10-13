"""Bind administrator discovery to reviewed portable metadata without pinning path-dependent UIDs."""
import json

PROVIDERS = ('PostgreSql', 'SqlServer')


def unique_object(pairs):
    """Reject duplicate JSON fields instead of silently accepting the last value."""
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate administrator inventory field')
        result[key] = value
    return result


def metadata(record):
    """Preserve the entire native type and every trait; discard only UID and source location."""
    native_type, traits = record.get('type'), record.get('traits')
    if not isinstance(native_type, dict) or not native_type or not isinstance(traits, list):
        raise ValueError('Missing administrator native metadata')
    for trait in traits:
        if (not isinstance(trait, dict) or set(trait) != {'key', 'value'} or
                not isinstance(trait['key'], str) or not trait['key'] or not isinstance(trait['value'], str)):
            raise ValueError('Invalid administrator native trait')
    return {'type': native_type, 'traits': sorted(traits, key=lambda item: (item['key'], item['value']))}


def metadata_identity(record):
    """Retain JSON type distinctions, including false versus zero, during exact comparison."""
    return json.dumps(metadata(record), sort_keys=True, separators=(',', ':'), ensure_ascii=True)


def load_inventory(path):
    """Require version two and exact agreement between names, providers and portable metadata."""
    value = json.loads(path.read_text(), object_pairs_hook=unique_object)
    expected = {'schemaVersion', 'metadata', *PROVIDERS}
    if not isinstance(value, dict) or set(value) != expected or type(value['schemaVersion']) is not int or value['schemaVersion'] != 2:
        raise ValueError('Unsupported administrator inventory')
    names = []
    for provider in PROVIDERS:
        cases = value[provider]
        if not isinstance(cases, list) or not cases or any(not isinstance(name, str) or not name for name in cases):
            raise ValueError('Invalid administrator inventory names')
        names.extend(cases)
    records = value['metadata']
    if len(names) != len(set(names)) or not isinstance(records, dict) or set(records) != set(names):
        raise ValueError('Administrator names and metadata disagree')
    for provider in PROVIDERS:
        for name in value[provider]:
            record = records[name]
            if not isinstance(record, dict) or set(record) != {'type', 'traits'} or record != metadata(record):
                raise ValueError('Invalid administrator inventory metadata')
            if [trait['value'] for trait in record['traits'] if trait['key'] == 'Provider'] != [provider]:
                raise ValueError('Administrator inventory provider differs')
    return value


def require_native_inventory(raw, inventory, *, complete=False):
    """Match every discovered record; full discovery must also exhaust the reviewed inventory."""
    value = json.loads(raw, object_pairs_hook=unique_object)
    if (not isinstance(value, dict) or type(value.get('schemaVersion')) is not int or value['schemaVersion'] != 1 or
            not isinstance(value.get('tests'), list) or not value['tests']):
        raise ValueError('Unsupported administrator discovery')
    names, uids = set(), set()
    for record in value['tests']:
        if not isinstance(record, dict):
            raise ValueError('Invalid administrator discovery case')
        name, uid = record.get('displayName'), record.get('uid')
        if (not isinstance(name, str) or not isinstance(uid, str) or not uid or name in names or uid in uids or
                name not in inventory['metadata'] or metadata_identity(record) != metadata_identity(inventory['metadata'][name])):
            raise ValueError('Administrator discovery differs from reviewed metadata')
        names.add(name)
        uids.add(uid)
    if complete and names != set(inventory['metadata']):
        raise ValueError('Administrator discovery omits reviewed cases')
