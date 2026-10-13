# T029: operational convergence and acceptance boundaries

T027 is accepted in PR #33 (`ab8b62d`). T028's automated scope is accepted in PR #34 (`fafc98d`) with its full exact-head evidence recorded in [MFA evidence](mfa-evidence.md). This packet completes T029's reviewable operational handoff and corrects a telemetry-registration gap; it does not invent human results or close whole-increment acceptance.

## Reviewed operational findings

- The custom ceremony/cleanup meters existed but production OpenTelemetry subscribed only to ASP.NET instrumentation. Administrator feature setup now subscribes the exact two approved names. Names remain feature-owned/internal; shared ServiceDefaults has no dependency on persistence. Pilot activation alone does not subscribe, and no wildcard accepts additional meters.
- The shared issuer runbook incorrectly described `Pilot=false` as sufficient for health-only behavior. It now requires both independent feature switches to be false and links the four-combination table.
- Recovery guidance now distinguishes lost password admission, lost committed redemption/binding, rereadable still-bound setup GET, lost completion, lost regeneration, abandonment and all-factor loss. No generic retry or reset bypass is introduced.
- The [operations runbook](../../docs/operations/administrator-accounts.md) explains WSL2 setup, explicit bootstrap, local-only authority, supported journeys, fixed incident diagnostics, metric semantics, cleanup/capacity/time behavior, durable keys/security state and the limits of restart/restore evidence.
- The [manual assessment](../../docs/operations/administrator-manual-assessment.md) and [result template](../../docs/operations/administrator-manual-results-template.md) require actual named Windows/WSL2 observations, both providers, exact tool versions, safe defect/retest attribution and explicit `not_run`/`blocked` outcomes. Pilot and administrator assessment scopes remain separate.

## Exporter regression

Four native host cases exercise every pilot/administrator switch combination through actual production composition. The test hook attaches only an in-memory manual metric reader; it never supplies `AddMeter`. Tests resolve the host-owned `MeterProvider`, emit real ceremony outcomes/backlogs and invoke the real cleanup failed-pass path, then require a bounded successful flush. Enabled hosts export exact meter/instrument/type/tag/value sets; disabled hosts export none despite identical emissions. An unapproved administrator-namespace canary rejects accidental wildcard subscription. A nonparallel collection and dormant timer eliminate interference from global static meters and scheduled ticks.

The failed-pass test does not emit or fabricate `cleanup.deleted`. Real cleanup/deletion transactions remain established by the provider suites. The export regression does not claim a working external collector, dashboard, alert, production SLO or exact committed-audit accounting.

Host inventory increases from 334 to 338 cases: unit total 399, ordinary .NET matrix 1,872. Provider/browser identity catalogues, eight administrator/seven issuer mutants, 45 coverage inputs, nine floors and all deadlines remain unchanged. Native execution, full CI and review results are recorded below when completed.

## Status and remaining work

Local verification passed: all 399 unit cases (25 Core, 338 Host, 5 Architecture, 31 AppHost), 133 Python tooling controls, full solution build, static quality rules with unchanged legacy baseline, AI instruction checks, assurance-register integrity and new relative-link targets. A deliberate omission of the ceremony-meter subscription caused exactly the two administrator-enabled cases to fail while disabled controls passed; restoring production registration passed all four. This proves the exporter test detects the original gap.

Independent source reviews of test/composition, security/architecture and operator/manual documentation converged. Documentation findings corrected the manual-case MFA-family mappings and the shared pilot runbook's health-only wording. No unresolved source finding remains. Complete exact-head CI is the delivery gate. [PR #35](https://github.com/mosesparadise/OpenIdentityAdmin/pull/35) is the authoritative final review, run and normal-squash record; this source checkpoint does not predeclare that result.

Human Windows/password-manager/screen-reader observations are unexecuted. T028/T029 and their T017/T018 umbrella therefore remain unchecked for whole-increment acceptance, even when these scoped PRs merge. Next planned feature work is T019 usable-administrator/recovery-route design, then T020 current permission/governance policy and T021 both-provider last-administrator/stale-authority evidence. T019's design must precede its dependent implementation. Admin API/BFF/React, authenticated documentation/vendor activation, passkeys, legacy code-quality debt and independent security/assurance/certification gates remain on the roadmap. No production deployment or external declaration is made.

## Delivery correction

Run `38337369524` at `fc46ccc` rejected two unnecessary imports through the full IDE0005 style gate. The local build/quality-rule/whitespace checks had passed but were not equivalent to that full style command. Both imports were removed without changing behavior. The complete `python3 scripts/check-static-analysis.py` command then passed locally, including the full solution build, whitespace verification, IDE0005 style verification and quality controls. The attempt is not accepted as a clean matrix; final delivery requires the complete exact-head run recorded in PR #35.
