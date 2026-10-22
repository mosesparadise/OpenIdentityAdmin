"""Apply only the exact A-009 WebKit evidence deferral after complete native inventory validation."""

CERTIFICATE_METHOD = ('IdentityPlatform.Issuer.Tests.Features.Browser.LoginBrowserTests.'
                      'Given_ExplicitBrowserTrust_When_UnrelatedCertificateIsPresented_Then_CertificateValidationRejectsIt')
PROVIDERS = ('PostgreSql', 'SqlServer')
ENGINES = ('chromium', 'firefox', 'webkit')


def select_required(cases, engine, provider):
    """Retain every other identity; a renamed/missing/duplicate deferred case fails closed for review."""
    if engine not in ENGINES or provider not in PROVIDERS:
        raise ValueError('Unknown browser deferral scope')
    if not cases or any(value[1] != provider or value[2] is not True for value in cases.values()):
        raise ValueError('Browser deferral requires a complete single-provider browser partition')
    if engine != 'webkit':
        return dict(cases), {}
    name = CERTIFICATE_METHOD + '(provider: "' + provider + '")'
    deferred = {uid: value for uid, value in cases.items() if value[0] == name}
    if len(deferred) != 1 or len(cases) <= 1:
        raise ValueError('A-009 exact certificate case is missing or ambiguous')
    return {uid: value for uid, value in cases.items() if uid not in deferred}, deferred


def receipt(deferred, engine, provider):
    """Emit explicit nonacceptance identities retained beside native reports, without claiming skipped cases passed."""
    return {'amendment': 'A-009', 'browser': engine, 'provider': provider, 'status': 'deferred_not_run',
            'cases': [{'uid': uid, 'name': value[0]} for uid, value in sorted(deferred.items())],
            'cause_established': False, 'acceptance': False}
