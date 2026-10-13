# T026 — restricted recovery and account security

Status: implemented in [PR #31](https://github.com/mosesparadise/OpenIdentityAdmin/pull/31), with final CI acceptance pending on accepted T025. The approved [MFA design](mfa-design.md) remains authoritative; this packet adds no emergency reset, management grant, new host or package. Native manager ownership, explicit Features/Setup and thin production entry points remain unchanged.

## Outcome and boundaries

An enabled, unsuspended administrator may recover with a fresh password and one unused recovery code. This grants only a bound replacement interaction, revokes old local authority and enters RecoveryRequired. It never issues a full-session cookie or records successful OTP authentication. Remaining recovery verifiers retain their generation so another fresh password/code attempt can recover from expired or lost staging. All-factor loss remains blocked and bootstrap stays consumed.

A current full account session may start voluntary authenticator replacement or recovery-code regeneration using a fresh password plus previously unused current TOTP, bound to exactly one action. Abandoned voluntary replacement preserves the old factor. Confirming a new factor installs exactly its proven destination generation and retains its replay reservation. Regeneration preserves the current factor/replay ledger while replacing all recovery verifiers. Codes appear once after audited commit; a lost response requires a new explicit authenticated journey.

Logout GET is confirmation only. Its CSRF-protected POST revokes exactly the current stored administrator session and commits its audit before clearing the named cookie. No OpenID Connect or BFF logout acceptance is implied.

## Implementation contract

- Keep account Razor slices beneath AuthServer/Features/Accounts and native mechanisms beneath Infrastructure/Features/Identity/Journeys and Ceremonies. Reuse exact forms, browser bindings, protected stores, generation allocation, replay, audit and secret-safe presentation.
- Add nullable persisted authorization kind/time/source-factor generation and initiating session binding to interactions, with both-provider migrations and readiness updates. Null/legacy state never authorizes a replacement stage. Only actual native recovery redemption or replay-reserved old-factor proof can establish that stage.
- Factor current-session validation into shared read-only checks while preserving ordinary validation's private metadata-only commit. Sensitive mutations reread authoritative state under the owning account lock and require a private transaction-bound capability; browser claims or cached request status alone never authorize them.
- Represent native recovery redemption with its own private single-use capability. It must bind account, transaction, interaction, recovery generation and fresh password. Never fabricate a TOTP proof to reset a budget or issue a cookie.
- Rotate public interaction ID/correlation within its existing row and browser budget. Carry original password time and absolute expiry. Dispose predecessor material selections, save changed bindings and reacquire before staging fresh ciphertext for a newly allocated destination generation. Old bindings cannot resume; retained capacity does not require allocating another row.
- Preserve the established governance/admission/browser/account lock order. Capacity checks precede proof consumption or authority mutation. Revocation deliberately preserves only an authorized transitioning slot and cannot restore stale tracked predecessors.
- Recheck eligibility, originating session where required, clock/freshness and expiry after awaited native work. Successful native writes, invalidation, audit and response ownership share one transaction. Dispose it before releasing codes or a one-shot full-session grant.

## Verification and traceability

ADMIN-003–006/009–012 and FR-07/12/17–19/21–24 map to the approved MFA-05–13 case families. Tests use Given_When_Then names, independent native-code expectations, both real providers and observed contention; no required skip or retry counts as acceptance.

| Story | Required cases |
| --- | --- |
| Recovery | Password-only denial; one-code restricted entry; remaining-code preservation; same-slot binding rotation; exact destination completion/replay; same/different-code races; stale/expired/foreign/suspended/disabled state; audit/native/cancellation rollback; lost response; final-code/all-factor loss. |
| Security | Current-session and action binding; fresh password/unused TOTP; abandoned replacement; regeneration/replay; capacity before proof; observed races and final freshness; atomic failures; exact-session CSRF logout and inert GET. |
| Browser | Recovery/replacement/regeneration/logout on both providers and Chromium/Firefox/WebKit; light/dark and compact/desktop, keyboard/focus/axe, native no-script forms and secret-history controls. No screenshots, traces, HTML dumps or secret-bearing failure output. |
| Upgrade/quality | Both native migrations and schema readiness; legacy null authorization denies; static analysis/XML/size/literals; strict discovery, required coverage inputs and meaningful seeded mutations. |

Reuse the existing Administrator project, fixtures and exact-inventory gates. Add Recovery and Security HTTP stories without expanding the ordinary issuer invocation. Split longer administrator browser stories when required; every invocation retains the 660-second limit and every added coverage input is mandatory. New mutation targets use an explicit feature-path allowlist and must fail at the demonstrated security defect, not compilation or unrelated exceptions.

Independent architecture and test preflight accepted these boundaries without a new approval dependency. Architecture/security, test, UX and performance reviewers must converge after implementation. T026 remains unchecked until actual provider/browser/full-CI evidence and normal squash merge. T028's actual paste/autofill and human-assessment obligations remain explicit; this packet makes no certification or public-release claim.

## Implemented evidence and outstanding acceptance

The native managers now implement the above transitions. Migrations `20261012231221_AdministratorRecovery` (PostgreSQL) and `20261012231226_AdministratorRecovery` (SQL Server) add nullable proof provenance and session binding with a complete-tuple CHECK constraint. No legacy interaction receives inferred proof. Upgrades preserve previously accepted enrollment/login data.

The first expanded local PostgreSQL run passed all 33 then-present Recovery/Security cases (zero skips, 353.741 seconds), including two-node observed lock contention, response loss, denied audit storage, failed native user validation after redemption, cancellation and freshness rollback. Follow-up tests add eight-live-session replacement, retained-row controls, immutable transaction-bound session proof, final initiating-session expiry, actual CSRF rejection on all eight routes and previous-schema upgrade. Final native discovery is the authority for inventory counts; full both-provider/three-engine CI remains required before acceptance.

The strict runner now has Recovery and Security HTTP stories and disjoint Accounts/Security browser stories. Their required coverage inputs cannot be omitted or relabelled. The six administrator mutations include native recovery postclaim rollback and exact-session logout revocation; each requires one demonstrated assertion failure plus clean original/restored controls. Existing seven issuer mutations remain required. Coverage floors and the 1,890-entry legacy quality baseline are unchanged.

Independent source reviews covered architecture/native authority, transaction/capacity, test oracles, UI/composition and fail-closed execution tooling. Corrections included mandatory rollback on native postclaim validation failure; immutable current-session capability binding; early rejection of absent staged ciphertext; exact remaining-verifier preservation; real foreign-session denial controls; visible no-script focus; keyboard-reachable security actions; disabled-route probes; and strict browser-story evidence controls. Source review does not substitute for CI, human accessibility assessment or certification.

The first CI run passed the complete 18 Recovery and 21 Security cases on both real providers. Browser evidence exposed an exact keyboard-navigation completion race in the test; the shared navigation helper now waits for the focused link destination before inspecting the new document. Independent review accepted that correction without retries or relaxed assertions. Final accepted evidence is recorded in [verification](verification.md#t026--restricted-recovery-and-account-security--13-october-2026-bst).
