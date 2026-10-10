# GitHub Spec Kit Implementation Plan and Constitution Draft

Use GitHub Spec Kit to turn the approved PRD into independently verifiable features that span the required projects. Features in Spec Kit describe user outcomes; Features directories in the code organize the corresponding use cases inside each project. They are complementary structures, not a rule to create one specification per .NET assembly.

Status: Planning only, tied to PRD version 0.2, 10 October 2026. Spec Kit has been inspected as a reference; it has not been installed or initialized for a new application, and no implementation command has been invoked.

## Approval boundary

The requesting user explicitly requires document approval before development. Until that approval, allowed work is repository review, requirements, architecture, contracts, research and documentation revisions. Scaffolding the solution, adding packages, writing an executable spike, generating migrations or invoking implement is outside the current authorization.

An approval record must capture the document version, approved scope, amendments, approving user and date. After the user authorizes development, perform the agreed implementation autonomously within scope. Do not manufacture repeated permission gates for routine feature work. Material changes to scope/security architecture return to the user with a concrete document amendment. Production deployment, certification publication/payment and external legal declarations remain separate actions. Duende migration is outside this greenfield scope.

## Tooling baseline and future setup

The reviewed Spec Kit release is v1.1.3, tag commit `bea765ce48b383c811872bfa2761a1e9600b7f32`; the development source inspected was commit `7666b7463ffc8b3dce49f75be017f163a46792fa`. Pin a verified release at kickoff and record it in the repository. Avoid relying on changing default-branch instructions.

The current CLI uses `--integration codex`; Codex receives skills under `.agents/skills` and invokes `$speckit-<command>`. Generic references may use `/speckit.<command>`, and Copilot's default skill form uses `/speckit-<command>`. These are agent prompts, not shell commands. Older advice using only `--ai` or assuming one slash syntax for every agent should not be copied into this plan.

After approval, the illustrative terminal setup is:

```bash
uv tool install specify-cli --from git+https://github.com/github/spec-kit.git@v1.1.3
specify init identity-platform --integration codex
cd identity-platform
specify check
```

For an already created target repository, use the pinned CLI's `specify init --here --integration codex` after inspecting its existing files. Do not overwrite user-modified agent instructions or templates automatically. The target repository name/owner is a kickoff input; the public Skoruba reference repository is not the default implementation destination.

Sources: [Spec Kit v1.1.3](https://github.com/github/spec-kit/tree/v1.1.3), [CLI reference](https://github.github.com/spec-kit/reference/core.html), [Codex integration and invocation syntax](https://github.github.com/spec-kit/reference/integrations.html), [installation guide](https://github.github.com/spec-kit/installation.html).

## Constitution draft

The first approved implementation action records these principles using `$speckit-constitution` and creates `.specify/memory/constitution.md`.

1. Approved requirements govern implementation. Every release requirement traces to a feature specification, acceptance criteria, tasks and verification evidence. Development does not begin before document approval.

2. Use Features per project. Keep a use case's transport, validation, execution and tests cohesive. Shared infrastructure is limited to stable reused mechanisms; avoid global business-service layers and generic repositories.

3. OpenIddict owns protocol mechanics. Minimal API handlers provide application decisions and complete through the appropriate OpenIddict scheme. Do not handcraft tokens or bypass protocol validation.

4. Identity and administrative authorization are explicit. ASP.NET Core Identity owns account credentials; current permissions govern management actions. Protect administrator recovery and last-administrator transitions.

5. Browser tokens remain server-side. React talks to the same-origin BFF using protected sessions and anti-forgery; management API authorization remains independent.

6. PostgreSQL and SQL Server have equal release status. Every persistence feature must include both migrations and meaningful integration verification against both providers.

7. Secrets and sensitive identity state are minimized. Use manager-based credential handling, durable external key management, least-privilege storage and sanitized audit/logging.

8. State-change effects are measurable. Specify behavior for cookies, token entries, authorizations, refresh and external offline validation separately. Retain cross-instance enforcement tests.

9. Operability is part of the feature. Include relevant health/metrics, failure behavior, schema evolution, recovery and runbook changes with the feature.

10. Prove behavior at the correct boundary. Prefer security, protocol, database and end-to-end tests over tests that only mirror implementation. No unverified security or conformance claims.

11. Use stable, pinned dependencies and reviewed updates. Preview framework or protocol packages require an explicit approved exception. Keep third-party notices and a dependency/license inventory.

12. Scope changes are visible. Unbuilt requirements and discovered limitations remain documented gaps. Automated convergence does not authorize adding unrelated scope.

## Feature workflow

For each feature after approval:

1. `$speckit-specify` converts the named user outcome and referenced FR/NFR IDs into `specs/NNN-feature/spec.md`, prioritized scenarios and measurable acceptance criteria.

2. `$speckit-clarify` resolves material gaps using the approved docs first, asking only for missing decisions that cannot be inferred.

3. `$speckit-plan` creates the technical plan, research, data model, contracts and quickstart. Include exact projects/slices, security effects and both-provider impact.

4. `$speckit-checklist` checks requirement quality and security/database completeness. It is not a substitute for running tests.

5. `$speckit-tasks` produces dependency-ordered tasks with affected paths, requirement IDs and verification commands. A task is complete only when its stated acceptance evidence exists.

6. `$speckit-analyze` checks specification/plan/task consistency and constitution compliance. Resolve critical ambiguities or contradictions before code execution.

7. `$speckit-implement` executes the approved tasks and verifies each meaningful behavior.

8. `$speckit-converge` assesses code against the feature documents, records missing work and feeds remaining tasks back to implement. Repeat within scope until the feature has no unbuilt accepted requirements.

9. Prepare a reviewable change with updated contracts/runbooks, test results, risk notes and requirement traceability. Repository/CI review gates do not imply a new user approval for every routine step after development is authorized.

The reviewed [Spec Kit plan template](https://github.com/github/spec-kit/blob/v1.1.3/templates/plan-template.md) includes `research.md`, `data-model.md`, `contracts/` and `quickstart.md`; `tasks.md` is a separate tasks-stage artefact. [Convergence](https://github.com/github/spec-kit/blob/v1.1.3/templates/commands/converge.md) identifies unbuilt work; it does not itself prove operational security or replace human review.

## Proposed feature sequence

| Feature | User-visible outcome and scope | Dependencies | Exit evidence |
| --- | --- | --- | --- |
| 001 Platform and issuer foundation | Add Aspire/ServiceDefaults and test/evidence infrastructure, evaluate project AI instructions, start hosts, load durable keys, apply schema and bootstrap on both DBs; establish a seeded code/PKCE flow and shared theme tokens | Document approval and constitution | Both providers initialize via Aspire; named schemes/pass-through, resolved OIDF-suite/mandatory-PKCE compatibility without silent weakening, baseline API-version tooling, coverage and agent evaluation evidence |
| 002 Secure administrator session | Administrator signs in through the BFF with code/PKCE/PAR, server-held tokens and protected API access | 001 | Two-node session/refresh tests, forged-header/CSRF rejection, current permission checks; FR-11 |
| 003 Scope and audience catalogue | Administrator defines API/identity scopes, audiences and claim-release policy | 001,002 | Exact scopes/audiences/claims from real tokens and UserInfo on both providers; FR-04 |
| 004 Application lifecycle | Create/profile/edit/clone/disable applications, manage credentials and provide versioned contracts, Scalar/Swagger UI and integration examples | 002,003 | Browser-to-token journey, credential replacement, disabled-client and concurrency tests; FR-01–03,16 |
| 005 Accounts and strong authentication | Manage users/roles, email confirmation/recovery, themed Razor login/recovery, TOTP, passkeys and safe OIDC federation | 001,002 | Account recovery, last-admin race, MFA and external-link tests; FR-05–07,14 |
| 006 Consent and access lifecycle | Consent, refresh eligibility/replay, grants/tokens/local sessions and revocation | 003–005 | prompt/max_age scenarios, user grant revoke and cross-instance five-second enforcement; FR-08,10 |
| 007 Device and protocol completion | Device flow, introspection, revocation, UserInfo and logout complete the advertised profile | 003–006 | Full protocol positive/negative matrix and relying-party interoperability; FR-09 |
| 008 Audit and configuration health | Search audit/diagnostics, run rules, resolve findings, show dashboards and key expiry | 002–007 | Atomic audit evidence, worker contention, rule lifecycle and UI investigations; FR-12,13,15 |
| 009 Production readiness | Complete theme/accessibility, performance, greenfield installation/recovery/key rotation, full regression evidence, independent ASVS assessment and OIDF submission package | 001–008 | Both-provider/browser release matrix, restore/load exercises, independent assessment and certification process evidence; FR-17–19,21–25, all NFRs |
| 010 Extended parity | Dynamic federation, relying-party session propagation, advanced credential overlap and reusable template | R1 accepted | Separate specifications and approval of any changed security/product scope; FR-20 and parity backlog |

Audit intent, authorization, observability and provider support are included as infrastructure and behavior in every earlier feature. Feature 008 adds the investigation UI and rule system; it does not defer security auditing until the end. Comprehensive tests, security-control mapping, shared theme/accessibility and Aspire verification also start with the first affected feature; feature 009 verifies completeness rather than introducing them at the end. Likewise, the foundation's seeded login is an internal demonstrator, not a production identity feature before 005–007 pass.

## Seed specification for the first complete vertical slice

Suggested prompt for `$speckit-specify` when reaching feature 004:

```text
Implement application registration under approved PRD FR-01, FR-02,
FR-03, FR-04, FR-12, FR-16 and FR-17.
An authorized administrator creates a confidential web application through
React and the BFF, chooses existing scopes, receives its credential once,
and uses that registration in an authorization-code plus PKCE flow.
Support both SQL Server and PostgreSQL. Reject public-client secrets,
duplicate client IDs, unsafe redirects, unsupported grants and stale edits.
Record a sanitized audit event atomically. Never expose a secret on reads.
Use Features inside the affected projects and the approved OpenIddict
protocol architecture. Do not implement later parity requirements.
```

Its technical plan must name the React screen, BFF route, Admin API create slice, manager-backed descriptor mapping, transaction/audit behavior, metadata persistence and cross-provider tests. This is a documented prompt for later use; it has not been executed.

## Required feature artefacts

```text
specs/004-application-lifecycle/
  spec.md
  plan.md
  research.md
  data-model.md
  contracts/
  quickstart.md
  tasks.md
  checklists/
  verification.md
```

`verification.md` is a project convention added to record requirement IDs, provider versions, commands, results and known limits. Link PRD requirements to feature scenarios, tasks, tests and the eventual change review. An empty checklist or generated task list does not count as verification.

## Approval record to complete

| Field | Current value |
| --- | --- |
| Documentation version | 0.2 dated 10 October 2026 |
| PRD and feature coverage | Awaiting user approval |
| Architecture and ADRs | Awaiting user approval |
| Protocol, greenfield data and versioned API design | Awaiting user approval |
| Verification and operations plan | Awaiting user approval |
| Spec Kit workflow and constitution | Awaiting user approval |
| React/Razor theme and accessibility | Direction confirmed; detailed design awaiting approval |
| AI skills and subagent drafts | Awaiting user approval; not installed |
| Certification and assurance plan | Recommended target selected; detailed plan awaiting approval |
| Approved amendments | None recorded |
| Development authorization | Not granted |

The user may approve the full package or request specific amendments. No application development starts until that response arrives.

## Expanded constitution and quality gates for version 0.2

13. Comprehensive verification accompanies every feature: requirement/invariant traceability, both-provider tests, browser E2E, failure/concurrency coverage and permanent reproducible-defect regressions. Numeric coverage is a floor, not a substitute for assertions.
14. Aspire is the standard local/distributed test composition, with real TLS/issuer validation, ephemeral fixtures, health deadlines and no blanket retries of security mutations.
15. Admin API versions are explicit; one supported contract per API surface drives Scalar/Swagger, with React generated from the BFF browser contract. Keep protocol routes separate and use the newest stable compatible package set.
16. React and Razor share slate/indigo light/dark design tokens; responsive states, WCAG 2.2 AA and visual/manual accessibility review are acceptance criteria.
17. Applicable ASVS L3 and protocol control evidence is mandatory. Target Basic OP/Config OP certification for the released configuration; never equate AI findings or internal tests with external certification.
18. Use the reviewed project-specific AI skills and relevant independent reviewers. Preserve source/test evidence, inherited model choices, host permissions and the main agent's integration accountability.

After approval, install the draft instruction-only skills into `.agents/skills/` and reviewer TOML files into `.codex/agents/`, merging repository guidance safely. Validate their schemas/discovery and evaluate seeded defects before relying on them. These additions complement the pinned Spec Kit commands and must not overwrite them.
