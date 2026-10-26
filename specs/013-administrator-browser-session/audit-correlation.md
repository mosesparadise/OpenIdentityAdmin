# B3 cross-store audit provenance

Status: implementation candidate, 26 October 2026. Required provider execution, exact-head checks and final review are pending. This prerequisite supports the user's selected T012/T016/T023 batch; none of those tasks is complete merely because this candidate exists.

## Contract and boundaries

FR-011/012 and the browser-session contract require safe linkage between issuer and BFF audit events. Existing independent GUIDs identify each local operation but do not establish that relationship. The implementation adds explicit nullable `RelatedCorrelationId` fields to `SecurityAuditIntent` and `BrowserSessionAudit`. Existing event identifiers, actors, targets, correlations and history are preserved. Four native EF migrations append one nullable GUID column per store/provider; existing records remain unlinked rather than acquiring invented provenance.

The BFF receives its actual operation GUID from `BrowserLoginCompletion.ReserveCorrelationAsync` only after the reservation commits and its transaction disposes successfully. The private `BrowserLoginReservation` passes this immutable receipt to the exact native challenge. `NativeClientBackchannel` adds `Identity-Browser-Audit-Correlation` only on that challenge's one permitted PAR request. The field is not an OpenIddict parameter: native request parameters can enter protected request-token metadata and the restored authorization request. It is not a token claim, browser field, authentication property or client-wide default.

The header accepts exactly one nonempty lowercase GUID in 32-character `N` format. Duplicate, combined, noncanonical, caller-owned and late-substituted values deny. HTTP header names remain case-insensitive. The sender rechecks the exact request, destination, registration, body and correlation around asynchronous serialization. Ordinary B2 and token sends require the correlation header to remain absent.

After real native fixed-client authentication, `BrowserCompletionPar` freezes the original incoming header before form parsing and checks it again afterward. `IssuerParCompletion` additionally requires the genuine completed intent/proof/source binding and preserves the frozen header across native work. After actual request-token persistence, it stages `administrator-browser-audit-link` alongside the existing attachment and audit in the same owned Platform transaction. The link has BFF operation `CorrelationId`, issuer continuation `RelatedCorrelationId`, fixed `protocol-client` actor and no secret selector or digest in its audit fields. Failure or uncertain acknowledgement cannot trigger another automatic send.

This metadata is authenticated client-asserted provenance, not independent issuer proof of the BFF database's contents. It grants no identity, session, permission, ceremony completion or lifetime. No browser-supplied header is copied into the private send capability. A compromised fixed client is not made trustworthy by choosing a valid GUID.

## Logout source history

Local BFF logout retains its new operation GUID and links to the previous login GUID only when the actual current ticket has authenticated actor provenance. Anonymous cancellation leaves the relation null. The issuer's real serialized `JourneyLogout` resolves the attached continuation for the exact authenticated account/source session and records its correlation in the same revocation/audit commit. Ordinary standalone sources remain unlinked.

Attached continuation retention already extends through original source expiry plus tolerance. Logout may therefore link a still-live source after the 300-second login intent expires. Cleanup removes expired continuation state without erasing durable audit links. Existing operation IDs and generation transitions remain unchanged.

These edges establish common authenticated source history. They do **not** pair a particular BFF logout request with a particular issuer logout request or acknowledge its outcome. No correlation is accepted from an end-session query. The browser return remains `ReturnedUnverified`; only independent issuer effects prove actual revocation.

## Verification obligations

| Boundary | Controls and required evidence |
| --- | --- |
| Header parsing and private send ownership | `BrowserAuditCorrelationTests`: canonical positive control; missing/empty/uppercase/combined/duplicate denial; unowned defaults; late substitution; ordinary send absence. |
| Real native admission and attachment | `NativeBrowserAuditTests`: both providers; malformed/header-only/unauthenticated demand; substitution after real native token persistence; restored valid control. |
| Genuine completion binding | `BrowserAuditPairingTests`: two actual password/MFA completions cannot be cross-paired by carrying canonical audit metadata. |
| Atomicity and uncertainty | Existing native audit-permission, replay/race/capacity and lost-response controls now include the typed link in their independent snapshots; `BrowserAuditCommitTests` targets only the actual PAR commit after token persistence. |
| Cross-store product provenance | `BrowserAuditHistory` compares the genuine BFF establishment audit, issuer attachment link and exact-source logout audit. Existing remote logout fixtures verify the local logout's related login correlation independently. |
| Retention and anonymous boundaries | `BrowserAuditLifecycleTests`: live-source logout after intent expiry; actual continuation cleanup preserving audit edges; anonymous cancellation has no source ancestry. Ordinary source logout controls require a null relation. |
| Compatibility | Native discovery must preserve every prior identity/trait; both-provider migrations/history/conformance, all existing browser stories, coverage inputs/floors and mutation controls remain required. |

The in-process native issuer bridge now forwards actual native request headers; it does not invent correlation metadata. Real HTTPS fixtures use the native network sender unchanged. This candidate does not itself complete the remaining browser secrecy, storage, negative journey, stale-tab or accessibility matrix.

Architecture and security planning reviews accepted the bounded design. Initial source review identified the pre-form-read freeze gap; the candidate now captures and rechecks the incoming value before/after that await. Final source/test/API/migration and raw-evidence dispositions remain pending. Initial local build attempts rejected formatting; corrections preserve strict build rules. No failed run is acceptance evidence.

Production scheduling, fleet/restore procedures, central audit export/retention, A-009 restoration, six historical findings, 48 human rows, 345 not-assessed controls and activation/public-release gates remain open.

## Local candidate checks

The reviewed implementation builds with zero warnings/errors. All 1,532 Host cases pass with zero skips, including 13 new controls; the focused header/native-policy selection passes 26/26. Two pure provider-witness controls pass without database execution. Native discovery preserves all 1,893 administrator identities and traits and adds 18 (1,911 total), while Persistence preserves all 20 and adds four. BrowserSessions grows to 322/provider (Foundations167, Product98, Lifecycle57); Persistence12/provider. All 227 Python controls and current code-quality checks pass; one resolved legacy public-documentation allowance is removed, and no allowance grows.

The frozen compatibility witness now requires the exact new Platform audit column and all four exact history IDs, preserving its original producer and every old row. Separate immediate-prior-schema tests populate Platform and Sessions audit tables, then compare every original physical value plus one appended null and complete migration history rows (including ProductVersion) after both the first and repeated actual upgrade. Their real PostgreSQL/SQL Server execution is pending hosted verification. Source review found and corrected the initial history-ID-only limitation before acceptance.

Architecture, security, API and test source reviews found no remaining product design blocker after the recorded corrections. Both-provider execution, raw reports, browser regressions, 60 coverage inputs/15 unchanged floors and all 64 existing mutation controls remain required. Local checks do not close this evidence gap.
