# Initial login visual baselines

These four credential-empty PNGs were captured by GitHub Actions run38060004485 at692fe98, in the PostgreSQL profile, then inspected individually by the lead and independent UX reviewer. Both approved them as candidate initial-screen baselines without a confirmed visual defect. No credentials/tokens are present.

Canonical comparison environment: workflow Ubuntu24.04, Microsoft.Playwright1.63.0 pinned Chromium, default1x scale/system fonts, reduced motion,900px viewport height, widths320/1280, explicit light/dark theme, full-page PNG, animations disabled and caret hidden. The application uses same-origin embedded assets and no network font downloads. The same files apply to both providers because the displayed content is provider-independent.

The test retains the actual capture before comparing SHA-256 of the complete deterministic PNG. Zero tolerance intentionally detects even small rendering changes. This comparison is canonical Linux/Chromium evidence, not a portable browser/OS image equivalence claim. Browser/font/runner or design changes require inspecting new captures against these files, recording a review, and deliberately updating baselines; never auto-bless a failure or loosen the comparison just to obtain a pass. The first comparison run remains pending until CI reports it.

These files cover only the initial empty login screen. Error/expired/freshness/focus/system states, Firefox/WebKit comparisons and manual assistive-technology/password-manager review remain T027 and release work. Passing these snapshots does not establish WCAG conformance.
