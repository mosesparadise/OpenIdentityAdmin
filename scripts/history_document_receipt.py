"""Validate one finite document observation without granting acceptance or navigation ownership."""
import json
import xml.etree.ElementTree as ET

FIELDS = {'category', 'prior_verified', 'commits_at_catch', 'commits_before_read', 'commits_after_read',
          'responses', 'same_frame', 'posts', 'document'}
DOCUMENT = {'CurrentRoot', 'InternalError', 'ErrorCode', 'Target', 'Location', 'Ready', 'Secret', 'Recovery'}
COUNTS = {'commits_at_catch', 'commits_before_read', 'commits_after_read', 'responses', 'posts'}
CATEGORIES = {'Read', 'NoCacheMiss', 'Timeout', 'Cancelled', 'Unavailable', 'Unexpected', 'CleanupIncomplete'}


def unique_object(pairs):
    """Reject duplicate names instead of silently replacing finite evidence."""
    result = {}
    for key, value in pairs:
        if key in result:
            raise ValueError('Duplicate document evidence field')
        result[key] = value
    return result


def require_document(path):
    """Retain inaccessible or mismatched observations explicitly; only exact finite schema is permitted."""
    root = ET.parse(path).getroot()
    lines = [line for output in root.findall('./{*}Results/{*}UnitTestResult/{*}Output/{*}StdOut')
             for line in (output.text or '').splitlines() if line.startswith('history-document ')]
    if len(lines) != 1 or len(lines[0]) > 2048:
        raise ValueError('Expected one bounded document evidence record')
    value = json.loads(lines[0].removeprefix('history-document '), object_pairs_hook=unique_object)
    if not isinstance(value, dict) or value.keys() != FIELDS or value['category'] not in CATEGORIES:
        raise ValueError('Unknown document evidence fields or category')
    if any(type(value[key]) is not int or not 0 <= value[key] < 1000 for key in COUNTS):
        raise ValueError('Invalid or saturated document observation count')
    if any(type(value[key]) is not bool for key in ('prior_verified', 'same_frame')):
        raise ValueError('Invalid document observation flags')
    document = value['document']
    if document is not None and (not isinstance(document, dict) or document.keys() != DOCUMENT or
                                 any(type(flag) is not bool for flag in document.values())):
        raise ValueError('Invalid document projection')
    if value['category'] == 'Read' and document is None:
        raise ValueError('Read result lacks its atomic projection')
    if value['commits_at_catch'] > value['commits_after_read'] or value['commits_before_read'] > value['commits_after_read']:
        raise ValueError('Document event counts moved backwards')
    supported = (value['category'] == 'Read' and value['prior_verified'] and value['same_frame'] and
                 value['commits_before_read'] == value['commits_after_read'] == 1 and value['responses'] == 0 and
                 value['posts'] == 1 and all(document[key] for key in DOCUMENT - {'Secret', 'Recovery'}) and
                 not document['Secret'] and not document['Recovery'])
    return {'document_observation': value, 'document_identity_supported': supported,
            'navigation_ownership_proved': False, 'acceptance_credit': False, 'causal_closure': False}


def combine_receipts(navigation, document):
    """Keep original navigation completeness distinct from the new diagnostic's overall completeness."""
    return navigation | document | {'navigation_evidence_complete': navigation['evidence_complete'],
                                     'evidence_complete': navigation['evidence_complete'] and document['document_identity_supported']}
