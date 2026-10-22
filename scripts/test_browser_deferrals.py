"""Adversarial controls for the narrow development deferral; unrelated browser tests remain mandatory."""
import unittest
from browser_deferrals import select_required, receipt


class BrowserDeferralTests(unittest.TestCase):
    def cases(self, provider):
        # Independent full identity: changing the selector constant cannot silently change this obligation.
        method = ('IdentityPlatform.Issuer.Tests.Features.Browser.LoginBrowserTests.'
                  'Given_ExplicitBrowserTrust_When_UnrelatedCertificateIsPresented_Then_CertificateValidationRejectsIt')
        return {'negative': (method + '(provider: "' + provider + '")', provider, True, 'LoginBrowserTests'),
                'positive': ('IndependentTrustedBrowserCase', provider, True, 'LoginBrowserTests')}

    def test_exact_case_only_is_deferred_on_both_webkit_providers(self):
        for provider in ('PostgreSql', 'SqlServer'):
            cases = self.cases(provider)
            required, deferred = select_required(cases, 'webkit', provider)
            self.assertEqual({'positive': cases['positive']}, required)
            self.assertEqual({'negative': cases['negative']}, deferred)
            report = receipt(deferred, 'webkit', provider)
            self.assertEqual('deferred_not_run', report['status'])
            self.assertFalse(report['acceptance'])
            self.assertFalse(report['cause_established'])
            self.assertEqual(cases['negative'][0], report['cases'][0]['name'])

    def test_other_engines_retain_the_certificate_test(self):
        cases = self.cases('PostgreSql')
        for engine in ('chromium', 'firefox'):
            self.assertEqual((cases, {}), select_required(cases, engine, 'PostgreSql'))

    def test_inventory_drift_cannot_expand_deferral(self):
        for fault in ('missing', 'duplicate', 'rename', 'wrong-provider', 'not-browser', 'empty', 'only-deferred'):
            with self.subTest(fault=fault):
                cases = self.cases('SqlServer')
                if fault == 'missing': del cases['negative']
                if fault == 'duplicate': cases['duplicate'] = cases['negative']
                if fault == 'rename': cases['negative'] = ('RenamedCase', *cases['negative'][1:])
                if fault == 'wrong-provider': cases['positive'] = ('Other', 'PostgreSql', True)
                if fault == 'not-browser': cases['positive'] = ('Other', 'SqlServer', False)
                if fault == 'empty': cases = {}
                if fault == 'only-deferred': del cases['positive']
                with self.assertRaises(ValueError): select_required(cases, 'webkit', 'SqlServer')

    def test_unknown_scope_denies(self):
        for engine, provider in (('unknown', 'SqlServer'), ('webkit', 'Other')):
            with self.assertRaises(ValueError): select_required(self.cases('SqlServer'), engine, provider)


if __name__ == '__main__':
    unittest.main()
