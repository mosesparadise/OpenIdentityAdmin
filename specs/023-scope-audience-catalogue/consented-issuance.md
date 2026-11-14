# T010–T012 consented issuance and UserInfo

The user selected the next three tasks on 14 November 2026 after accepted T009.
Implement T010 consent, T011 native issuance and T012 UserInfo, including T013's
required fixed-BFF discovery compatibility and the applicable T014–T016 tests,
review and delivery obligations. Base: `f6c959a7cab1f6cb7b799d04348922d2bf259852`.
Existing M03-B/C and consented current email/email_verified approval governs scope.

## Implementation boundaries

- Freeze bounded ordinal requested scopes; reject duplicate, reserved, unassigned,
  disabled and conflicting-audience scopes. The existing explicit Allow/Cancel form
  approves all requested eligible scopes or none; no silent downscoping or inferred
  consent. OpenID-only applications retain their existing behavior.
- Bind the canonical policy digest through native cached request, protected form and
  native code. Legacy absent bindings are admissible only for the original exact
  OpenID profile with no retained references and current fixed native permissions.
  Recheck current native/catalogue policy after locks and before each commit.
- Render plain-text current scope labels, consent descriptions, audience and finite
  email disclosure. Protect the actual displayed policy through the existing form
  and publication boundaries; preserve native antiforgery and fresh password/MFA.
- Issue only native code/S256 and access/ID tokens. Exact granted scopes and at most
  one API audience derive from the bound policy. Email and boolean email_verified
  come from current Identity state only at emission, only for consented identity
  disclosure and only in ID token/UserInfo; access tokens carry neither.
- UserInfo GET/POST uses native access-token validation, exact current managed token,
  authorization, interaction, account, client and policy. No query token fallback,
  fixed-admin/pilot/machine profile or arbitrary claim projection. Use a distinct
  bounded read/publication receipt and native no-store/error formatting.
- Advertise only the implemented fixed UserInfo endpoint. The BFF accepts that exact
  endpoint while retaining both independent no-UserInfo-send fences. Update obsolete
  temporary assignment-hold UI text only when extended issuance is implemented.

## Required evidence

C07–C12: real both-provider consent/subset/audience/claim/denial journeys, independently
validated native tokens, policy/cache/code changes, actual cross-process evidence,
current UserInfo denials, email presence/absence/confirmation/change and original
publication/consumption deadlines. Preserve earlier native identities and assertions;
new extended expectations are explicit opt-ins to strict existing token oracles.
Retained Chromium/WebKit consent and BFF journeys, permanent fault/race regressions,
causal mutations, original coverage floors and exact-head full CI remain required.

Implementation is present on branch `143-managed-scope-consent`; acceptance is pending.
Do not close T010–T013 from this packet.
No local SQL startup retry is planned in the disk-constrained workspace. Preserve all
prior evidence, frozen producers and diagnostic branches. All six historical findings
and human/Windows/Firefox/A-009/refresh/vendor/production/public-release gates stay open.

## Implemented scenarios and local verification

| Controls/tasks | Permanent scenario families | Evidence required for acceptance |
| --- | --- | --- |
| C07/C08; T010 | ManagedRequestedScopeTests, ManagedConsentDriftTests, ManagedConsentBindingTests, ManagedScopeProcessTests | Actual protected displayed consent, same-revision drift, separate process/current-policy read under provider-observed governance contention |
| C07–C09; T011 | ManagedConsentProjectionTests, ManagedConsentHttpTests, retained token/replay/publication tests | Independent crypto oracle, exact scope/audience, current email pair or complete omission; no access-token email |
| C10/C11; T012 | UserInfoTransportTests, ManagedUserInfoTransportTests, ManagedUserInfoDenialTests, ManagedUserInfoPublicationTests | Real GET/POST header/form transport, native safe denials, changed current authority and actual callback/commit/disposal/deadline faults |
| C12; T013 dependency | NativeClientSharedDiscoveryTests, retained native client suite, ManagedUserInfoTransportTests | Exact optional shared discovery, both no-send fences, genuine fixed-BFF login/logout |
| C07–C12; T014–T016 | Existing ApplicationScopeBrowserTests extended; ten managed mutations | Both providers and Chromium/WebKit; Allow/Cancel, token/UserInfo, themes/axe/keyboard/reflow; causal bypass markers |

Local .NET 10.0.404 builds of Host and Administrator tests succeed without warnings.
The entire Host suite passes **1,967/1,967**, zero skipped. Actual native discovery adds
44 administrator cases while preserving every earlier 2,933 identity and full portable
metadata: 2,977 total (HTTP 1,360 PostgreSQL / 1,465 SQL Server; browser inventory
unchanged). Managed Admission/Issuance partitions are 63/82 per provider.
All **337 Python tooling controls** pass. Code-quality checks pass with unchanged
allowances, and the eight-skill/eight-reviewer instruction check passes. UI type checking
and deterministic asset verification pass. All 132 UI cases pass across the six suites
(the scope suite was rerun after replacing the obsolete temporary-hold text assertion). These local checks do not replace hosted
provider/browser/mutation/coverage evidence.

Architecture and security source review found no remaining blocker. Test review caught
and resolved the finite mutation ordinal bound, missing per-case selection traits and
noncausal UserInfo bypass markers. New markers require an independently verified leaked
profile (plus actual callback/commit/disposal witnesses for publication), rather than an
arbitrary unexpected status. Review acceptance remains conditional on runtime evidence.

Late current-authority changes fail closed: ordinary invalidation before projection is a
native bearer denial; ownership/storage/final source-snapshot faults remain safe503.
The separate-process test exercises real production policy reads with caches disabled,
not a fabricated cache hit. No local provider startup experiment was run. Earlier local
build/disk failures and superseded test receipts remain retained outside the repository.
