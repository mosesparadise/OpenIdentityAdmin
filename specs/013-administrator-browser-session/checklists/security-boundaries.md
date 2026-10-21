# Security/API requirements checklist — B3 planning

Created 21 October 2026. Reviewer-owned dispositions; these checks assess contract completeness and consistency, not executed behavior.

- [x] CHK001 Does the login intent bind both browsers, exact fixed client, actual completed ceremony and final exchange without reviving PAR/state? [FR-001/002; contract fresh-login sequence]
- [x] CHK002 Is original source expiry authenticated and strictly validated, with no token-expiry, opaque-sid or fresh-allowance substitution? [FR-003/004]
- [x] CHK003 Are native crypto/claim destinations and the new canonical local ticket allowlist explicit, with no permission snapshots or browser OAuth credentials? [FR-003/004/013]
- [x] CHK004 Do cookie/origin/antiforgery requirements cover sign-in, replacement, local logout, stale tokens and narrow protocol exceptions? [FR-006/007]
- [x] CHK005 Are callback/logout/replacement ordering and ambiguous commit specified without pretending B1 CAS or network delivery is atomic? [FR-005/008/009]
- [x] CHK006 Are local revocation, issuer session termination, browser return and future refresh/API consequences distinguished truthfully? [FR-009/010/015]
- [x] CHK007 Are expiry/quota/retention/cleanup/restore boundaries finite, fail-closed, owned and portable across both providers? [FR-011/012]
- [x] CHK008 Are actor provenance and secret-free audit/logging requirements testable without claiming completed central retention or human assurance? [FR-011]
- [x] CHK009 Does the endpoint contract define safe GETs, failure/status behavior and protocol separation without an open redirect or bearer fallback? [FR-001/006/007/010/013]
- [x] CHK010 Are Firefox, actual native integration, independent evidence and activation/release prerequisites preserved? [FR-013–015]

Independent planning review passed these items after the registration/PAR correction; see [verification](../verification.md). This is not runtime acceptance.
