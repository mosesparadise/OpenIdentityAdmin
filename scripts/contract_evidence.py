"""Fail-closed evidence readers for the pinned MTP and Node contract runners."""
import json
import xml.etree.ElementTree as ET


def catalogue(raw):
    document = json.loads(raw)
    if document.get('schemaVersion') != 1:
        raise ValueError('Unsupported contract discovery schema')
    tests = document['tests']
    result = {}
    names = set()
    for test in tests:
        uid, name = test.get('uid'), test.get('displayName')
        if not isinstance(uid, str) or not uid.strip() or not isinstance(name, str) or not name.strip() or uid in result or name in names:
            raise ValueError('Missing or duplicate contract identity')
        browser = any(trait.get('key') == 'Category' and trait.get('value') == 'Browser' for trait in test.get('traits', []))
        if browser != ('ViewerTests' in name):
            raise ValueError('Contract browser classification changed')
        result[uid] = (name,)
        names.add(name)
    if not result:
        raise ValueError('Empty contract discovery')
    return result


def require_node_report(path, expected):
    parser = ET.XMLParser(target=ET.TreeBuilder(insert_comments=True))
    root = ET.parse(path, parser=parser).getroot()
    cases = root.findall('./testcase')
    names = [case.get('name') for case in cases]
    if root.tag != 'testsuites' or len(expected) != len(set(expected)) or len(names) != len(expected) or set(names) != set(expected):
        raise ValueError('Node identities are omitted, duplicated or substituted')
    if any(list(case) or not case.get('name', '').strip() for case in cases):
        raise ValueError('Node case contains unsuccessful or unexpected evidence')
    comments = [(node.text or '').strip().split() for node in root if node.tag is ET.Comment]
    required = dict(tests=len(expected), suites=0, **{'pass': len(expected)}, fail=0, cancelled=0, skipped=0, todo=0)
    for name, expected_count in required.items():
        values = [parts[1] for parts in comments if len(parts) == 2 and parts[0] == name]
        if values != [str(expected_count)]:
            raise ValueError('Node summary counters are missing or contradictory')
    if any(node.tag is not ET.Comment and node.tag != 'testcase' for node in root):
        raise ValueError('Unexpected Node report structure')
