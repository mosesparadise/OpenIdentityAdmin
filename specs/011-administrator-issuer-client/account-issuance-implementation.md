# Account-bound native issuance continuation

18 October 2026, Europe/London. This continues the authorized T005–T009 batch (FR-001–009/012/013; SC-001–006). It is incomplete integration work on draft PR #66, not a separate helper delivery or task acceptance. Preserve the [verified PAR/admission checkpoint](native-consumption-implementation.md#verified-par-and-admission-checkpoint-18-october-2026), provider implementation/history and all earlier delivered ownership boundaries.

## Implemented source

Minimal authorize/token decisions use the actual native authentication context. Authorization reads the named full administrator cookie; exchange retains an independent clone of the protected native code principal. A private one-use decision binds the original HTTP/native request, endpoint, authenticated principal and parameter digest. It supplies opaque account/session selectors, never reusable admission authority. Original code scopes, resource, presenter, authorization/token identity and protected age must qualify before projection; resource/audience overrides are rejected, including empty-present raw query/form fields.

Fresh account issuance claims the already durable native PAR/code consumption receipt, plus the separate token-endpoint assertion receipt. Its transaction acquires governance/profile/account locks before tracked reads, reads complete live registration and source authority, and projects fixed claims with original password/factor times. It preserves a newly attached native authorization or the code's existing authorization, reloads its actual persisted descriptor and verifies the exact native created token rows. Final checks repeat source admission, native principal/request binding, original code proof and strict expiry without renewing authentication. Registration identity/revision must remain unchanged; a nondecreasing observation watermark is permitted.

Wrappers import the actual native authorization attachment and ASP.NET passthrough descriptors, preserving filters/order. The handoff receipt requires the real native skip on the exact outer/inner contexts, original scope/services/capture and unchanged parameter/header baseline. It binds one later sign-in. After native generation, formatting, commit and actual child cleanup, endpoint publication requires that receipt, the exact handled sign-in and frozen parent output. PAR retains its handled outer-request rule. The ordinary all-write-phases-committed gate is unchanged. Existing host security headers are preserved.

Success responses have endpoint-specific allowed fields and exact native bindings before formatting and again before commit. Authorization permits code/state/issuer only on the separately verified fixed query callback. Token exchange requires access token, ID token, Bearer type, bounded numeric lifetime and the two fixed scopes; extra credentials or identity fields cannot be frozen into success. Native rejection still produces the existing sanitized 503 where no verified error publication exists.

## Verification and review

Local full Host execution passes **832/832**, zero failures/skips, in 55.123 seconds. The 62 additions comprise 19 fixed request/code controls, 19 native handoff controls, two account composition/descriptor controls and 22 response-shape controls. Host and Administrator builds pass with zero warnings/errors. Source-quality checks require no new allowances. Selector/browser/coverage controls pass 48/48. Informational unused-import review found and removed one unused test import; final source checks accompany publication.

Actual native administrator discovery preserves all **1,077 existing UIDs, names, type signatures and traits** at the same executable path and adds **32 cases**, totaling **1,109**. The ProtocolAdmission partition becomes 44/provider; ordinary totals become PostgreSQL 489 and SQL Server 594, with unchanged 13 browser cases/provider. No timeout, 50-input coverage requirement, 15 module floor, historical test identity or mutation oracle is weakened.

The new real-provider cases use the existing protected bootstrap/enrollment journey, its actual one-shot named-cookie issuance, durable fixture issuer certificates, native ASP.NET request handler and actual endpoint SignIn result:

- Eight positive cases cross both providers with Platform, Application, Identity and Auditor bundles. They check native PAR/code/credential bindings, original source proof, exact callback state/issuer, minimal emitted claims, strict upper expiry bounds and a real clock advance after each admission before quota reservation. A fresh independent read checks the committed profile watermark. ID-token signature/issuer/audience/type/algorithm/expiry/nonce/access-token hash use a strict recipient cryptographic oracle; the production server's ID-token rejection remains intact.
- Eight pre-formatter response mutations cross authorize/token and extra refresh/identity fields. Actual hook reach is asserted outside the catch-all HTTP boundary; no body/location may escape, retained native row counts must match the independent prior observation and early consumption must remain redeemed.
- Sixteen final-read mutations cross both endpoints/providers with public subject, private resource, request state and identical principal-clone substitution. The interceptor requires the actual final `AdministratorSession` SELECT, the exact owning database and completed native formatting. Each test requires exactly one observed mutation, withheld output, issuance rollback and durable earlier redemption.

These provider cases are **compiled/discovered, not yet executed** at this source checkpoint. DefaultHttpContext exercises installed ASP.NET components; it does not prove live HTTPS transport or native-client callback interoperability. Independent architecture/security reviews corrected exact parent ownership, preserved header baselines, recipient validation context, response allowlisting and the final-read table matcher. No reviewer claim substitutes for execution.

## Remaining work

First run/review the new source on both providers and correct any evidence-backed failure. Then complete shared disabled/pilot/admin/combined production composition, raw form/method guards and pilot isolation; capability-bound native error publication after proven cleanup; real native client PAR/callback/no-retry/state integration; authority/revocation races and required adversarial/operation/mutation coverage. The one-shot registration command entry path still has 20 uncovered sequence points in the last published coverage report. Full T005–T009 dependencies, coherent B2 acceptance and exact-head review remain open. Do not squash this partial integration.

All six historical findings, 48 human rows and activation/public-release blockers remain open. Firefox is deferred only through internal B2 under A-007 and must return before B3 implementation, product activation or release. No local SQL/Docker experiment, production activation or merge is performed by this continuation.

### Hosted selection correction — 18 October 2026

Source `a9881110` run `39092047743` stopped ordinary test lanes before .NET execution: the independent lifecycle partition control retained the earlier administrator/provider totals. The 32 additive native account cases require administrator totals 489 PostgreSQL / 594 SQL Server and complete provider totals 844 / 949. The correction updates only those independent expectations; selection, inventory, assertions, deadlines and coverage floors remain unchanged. All 206 Python controls pass locally. This failed run supplies no native account execution acceptance; a new exact-source run remains required.

### Verified account checkpoint — 18 October 2026

Source `a5a0f463ae13be5a840a3d867ab1f754020b55d5`, tree `b2c8a74a1ac82af0b2c5451e62d7ce0fdbd0ccd9`, was tested as merge `d86ff31491ddd7ce57b3ac804cb0a0aa9096b905` against main `1ffe5ed`. [Run 39092523489](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39092523489) passes all 65 jobs. Independent raw review reconciles 927 unit, 88 admission (44/provider), and 44 foundation (22/provider) cases with zero failures/skips; all 965 prior reviewed identities remain and all 32 account additions executed. The positive cases reach both clock advances and validate native code/access credentials plus independent ID-token signature/issuer/audience/nonce/at_hash. Negative cases reach their exact injected fault, suppress output, roll back issuance, and retain earlier durable consumption.

All 50 mandatory coverage inputs and 15 floors pass independent recomputation. All 73 changed executable files appear; the previously recorded Migrator initialization entry-path zero-hit gap remains. This checkpoint excludes newer shared-host wiring, native BFF-client interoperability and B2 acceptance.

| Artifact | ID | SHA-256 |
| --- | --- | --- |
| foundation-unit | 12123655989 | `5f209682a834a083229800ad201b456f94583bc16ebb422e34bedd1f5c63c3ae` |
| foundation-PostgreSql | 12123373073 | `de7aa904fb626ff191e03b8f36600c04bc58cc24f05bfa89a27569b3cbd9a3ad` |
| foundation-SqlServer | 12124045834 | `a52f9e0c0d85e800fd61e5ed93fe6a33f4edc8e5f4bac1144d9eb8501eeba6a5` |
| foundation-administrator-PostgreSql-protocoladmission | 12122708953 | `dfdf97424654c76e50f261e6e0b6aceea4ce8e5de5611f06e48647eda666da42` |
| foundation-administrator-SqlServer-protocoladmission | 12123645546 | `cb8d57bfa378254214c6f46340e57ca7b794fa863c95d66e71c11af3a9a476e5` |
| foundation-coverage | 12124105224 | `6bbaf71a1cfaff6e335285a27a2520e5151362a780c62b68ee8a93463a78cae0` |
