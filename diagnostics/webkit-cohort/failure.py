"""Project only the existing fixed C# certificate failure format; unknown text stays private."""
import re
import xml.etree.ElementTree as ET

CATEGORIES = frozenset(('SEC_ERROR_UNKNOWN_ISSUER', 'SEC_ERROR_UNTRUSTED_ISSUER', 'SEC_ERROR_CA_CERT_INVALID',
    'MOZILLA_PKIX_ERROR_SELF_SIGNED_CERT', 'ERR_CERT_AUTHORITY_INVALID', 'Unacceptable TLS certificate',
    'Target page, context or browser has been closed', 'Missing X server', "Executable doesn't exist",
    'SEC_ERROR_UNTRUSTED_CERT', 'SEC_ERROR_UNKNOWN', 'SSL_ERROR_UNKNOWN', 'Provisiolal navigation canceled.',
    'Load failed', 'An SSL error has occurred', 'Connection terminated unexpectedly', 'Could not connect',
    'Connection refused', 'cancelled', 'canceled', 'Timeout', 'unclassified browser error', 'missing', 'empty'))
HEADER = re.compile(r'Native certificate evidence wait expired; navigation=([^;]{1,80}); exact failures=(\d{1,10}); unrelated failures=(\d{1,10}); page-closed=(True|False); browser-connected=(True|False|unknown); dropped=(\d{1,10}); events=\[([^\r\n]{0,6000})\]\.')
EVENT = re.compile(r'ordinal=([1-8]) elapsed-ms=(\d{1,19}) channel=(request|navigation) exact=(True|False) category=([^;]{1,80})')


def project(raw):
    assert len(raw) <= 2 * 1024 * 1024 and b'<!DOCTYPE' not in raw.upper() and b'<!ENTITY' not in raw.upper()
    root = ET.fromstring(raw)
    errors = root.findall('./{*}Results/{*}UnitTestResult/{*}Output/{*}ErrorInfo')
    result = {'category': 'none' if not errors else 'unknown', 'certificate_wait_frame': False,
              'certificate_assertion_frame': False, 'fixture_frame': False}
    if len(errors) != 1: return result
    stack = errors[0].findtext('{*}StackTrace') or ''
    result.update(certificate_wait_frame='CertificateObservation.RequireAsync' in stack,
                  certificate_assertion_frame='BrowserCertificateEvidence.RequireAsync' in stack,
                  fixture_frame='IssuerFixture.GetAsync' in stack)
    message = errors[0].findtext('{*}Message') or ''
    if len(message) > 8192: return result
    header = HEADER.fullmatch(message)
    if not header or header[1] not in CATEGORIES: return result
    events = []
    pieces = header[7].split('; ') if header[7] else []
    if len(pieces) > 8: return result
    for piece in pieces:
        event = EVENT.fullmatch(piece)
        if not event or event[5] not in CATEGORIES or int(event[2]) > 2**63 - 1: return result
        events.append({'ordinal': int(event[1]), 'elapsed_ms': int(event[2]), 'channel': event[3],
                       'exact': event[4] == 'True', 'category': event[5]})
    counts = list(map(int, (header[2], header[3], header[6])))
    if max(counts) > 2**31 - 1: return result
    if [event['ordinal'] for event in events] != list(range(1, len(events) + 1)): return result
    if len(events) + counts[2] != counts[0] + counts[1] + 1: return result
    return {**result, 'category': 'certificate-evidence-expired', 'navigation': header[1],
            'exact_failures': counts[0], 'unrelated_failures': counts[1], 'dropped': counts[2],
            'page_closed': header[4] == 'True', 'browser_connected': {'True': True, 'False': False, 'unknown': None}[header[5]],
            'events': events}
