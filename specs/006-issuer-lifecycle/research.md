# Lifecycle research — design evidence only

Status: 11 October 2026, no lifecycle tests executed.

| Finding | Local source / implication |
| --- | --- |
| Durable identity includes issuer URI | `IssuerProtectionRegistration.cs` derives the DP application discriminator from the canonical issuer. Keep the same origin; rebuilding a dynamic AppHost endpoint can invalidate cookies for the wrong reason. |
| Runtime credentials are purpose-separated | `IssuerCredentials.cs` imports one signing, one encryption and current/previous DP wrapping certificates. Only wrapping supports retained keys today; signing/encryption overlap requires an explicit later contract. |
| Aspire supports resource commands | Pinned Aspire.Hosting 13.6.1 XML/API exposes ResourceCommandService with stop/start/restart. Explicit stop→terminal/old-exit→start→new-generation/healthy gives stronger proof than a restart command's success. |
| Current fixture auto-login can hide failure | `IssuerFixture.cs` AuthorizeAsync submits credentials if redirected to the form and changes expected session metadata. Restrict it to setup/recovery; manually navigate the old-cookie oracle. |
| Current cleanup captures only initial processes | `LifecycleEvidence.cs` captures PID/start-time once. Replacement generations must be retained and verified on disposal, including partial failure. |
| Genuine RP already owns correlation/validation | `TestRelyingParty.cs` keeps single-use expiring browser-bound state, nonce/verifier and tokens server-side, and validates through public metadata. Preserve these controls in any shared test support. |
| Current suite budget is bounded | Existing issuer partition: 292/provider, 660-second outer timeout. PR #16 issuer observations: PostgreSQL347.889s / SQL Server523.495s; prior SQL577.695s. New lifecycle isolation avoids consuming uncertain headroom. |
| Local SQL image access may be blocked | This managed environment previously denied the SQL image redirect. Inspect current policy; no bypass or provider substitution. Normal GitHub CI supplies actual SQL evidence if local access remains blocked. |

Architecture and security reviewers have provided preliminary design findings about stable origin/stores, real process generation, accumulated cleanup, auto-login false positives and expiry-versus-replay oracles. The user confirmed the dedicated project and three sequential PRs. The concrete harness uses source-linked support, a separate invocation under existing provider coverage collection and unchanged time budgets; architecture review passes this design. These are read-only findings, not runtime evidence.
