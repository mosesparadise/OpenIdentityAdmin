# Internal ticket-store contract

No HTTP endpoint or public OpenAPI operation is introduced. Use native ASP.NET Core ticket serialization and a named BFF scheme at later integration; synthetic store probes are not authentication authority.

| Operation | Input/output | Contract |
| --- | --- | --- |
| Create | Native ticket, subject/source binding, optional shorter idle/absolute limits, required upstream expiry, cancellation → fresh handle/revision | Commit protected ticket and intent together. Reject bounds without reflecting input. Return only after commit. |
| Load | Canonical handle, cancellation → state/revision or unavailable | No renewal on read. Unknown/revoked/expired/tampered state gives no ticket. Distinguish operational failure internally; neither path permits access. |
| Renew | Handle, expected revision, replacement ticket for same binding, cancellation → updated/rejected | CAS only; preserve original authenticated idle duration and absolute/upstream constraints. No automatic retry, resurrection or changed binding. |
| Revoke | Handle, cancellation → committed/already absent or safe operational failure | No caller revision prerequisite. Local invalidation does not claim remote token/authorization revocation. |
| Cleanup | Captured UTC time, cancellation → count at most 100 | Recheck eligibility in deletion; no live-row removal or audit cascade. |

## Native ticket binding and time ownership

The store-owned scheme is `IdentityPlatform.Admin.Bff.Session`. Require that exact `AuthenticationTicket.AuthenticationScheme`, exactly one authenticated identity with the same authentication type, and exactly one `sub` and one `sid` claim. Their values must ordinally match the supplied subject and source-session binding. Create and replacement-ticket renewal reject mismatches, duplicates or additional identities; load revalidates the native ticket against the authenticated envelope. Later client integration must explicitly construct this canonical ticket from its validated principal; the store convention does not validate an OIDC response or grant administrative permissions.

Upstream expiry means the **source administrator authentication-session deadline**, not the access-token expiry or a fresh renewal allowance. Creation's immutable absolute cap includes that deadline. The store cannot observe later upstream revocation or policy shortening; B3/C/D must revalidate current eligibility at their authorization boundaries. Access/refresh token lifetimes remain separately enforced by their consumers.

The store owns native `AuthenticationProperties.IssuedUtc` and `ExpiresUtc`: on create/renew it writes the computed activity time and effective idle deadline into a private ticket copy before protection. Caller values cannot extend these fields, the authenticated original idle duration or the immutable absolute/source deadline. Load requires native properties to agree with the envelope. Other native properties/tokens survive serialization unchanged. Add negative tests for conflicting incoming lifetime properties and tampered persisted lifetime/binding, plus a five-minute idle policy preserved across renewal/restart.

Inject a clock for boundary evidence. Flow cancellation through database operations and owned cleanup; do not translate cancellation into successful empty results. Errors/telemetry use fixed categories without private input or raw exception messages. Retain raw test evidence privately and publish sanitized summaries with appropriate source/report hashes.

Protection configuration rejects missing/unsafe ring paths, invalid wrapping material, plaintext/corrupt ring entries and unavailable retained decryptors. Extract existing validated mechanics through narrow Infrastructure code where needed; preserve issuer controls and explicit credential/disposal ownership. Keep BFF's ring/application/purpose distinct; no ephemeral fallback.

Later browser integration requires a secure HttpOnly host cookie containing only an opaque reference, fresh handles after login/privilege changes, same-origin/CSRF/correlation protection, no browser OAuth tokens and accurate logout failure reporting. Those are B3/C criteria, not this store's capabilities.
