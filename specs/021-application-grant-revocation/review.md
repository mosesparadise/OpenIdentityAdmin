# Grant-revocation review and checkpoint

**Date**: 2026-11-05. **Stage**: requirements/research draft, not implementation acceptance.
**Baseline**: `dce682f9812d228b41ae7fad1dcb1f6636f40d38`.

## Independent research dispositions

Repository AGENTS.md requires architecture/test and affected security review for this
substantive feature. Reviewers inspected the baseline read-only; no runtime tests ran.

| Reviewer | Finding | Disposition |
| --- | --- | --- |
| Architecture (`callback_architecture_review`) | Native manager wrappers exist but Worker has no durable processor; native bulk calls are not bounded jobs. | Research defines a bounded processor prerequisite and short atomic transactions under existing serialization; no generic lease framework assumed. |
| Test (`final_test_review`) | Require both-provider atomic failure/restart/duplicate-worker tests, native states, real browser/Aspire, additive migrations and mutation coverage. | Research evidence matrix includes each obligation; no proposed case is reported as passed. |
| Security (`grant_security_review`) | Accepted intent must be distinct from expiring live-session authority; current persisted grants/account remain required. | GR-006/007/012 and research separate immutable approval, per-batch checks and explicit recovery. |
| Security | Protected IDs, disabled/expired cleanup eligibility and authorization-less tokens are easy to miss. | GR-003/005 and source findings include independent target/selection checks. |
| Security | Revision or wrong-audience denial does not prove native revocation. | Evidence plan requires causal native entry validation plus independent statuses and separate genuine issuer lifecycle controls. |
| All | Disabled versus enabled policy is unresolved. | GR-004 remains pending; no recommendation is attributed to the user. |

The previous expiry reviewer could not run because of capacity; the completed independent
security research above replaces that unavailable review, not with an invented pass.
Final test and security reviewers pass the bounded requirements/research draft only.
Architecture review identified the same continuation-actor ambiguity as security; GR-007
and research now explicitly check the actor of the effective continuation approval while
preserving earlier approvers as history. Security also required current authorized,
actor/action/target-scoped duplicate recovery, now explicit in GR-010 and SC-001.
No reviewer accepts implemented behavior; GR-004 remains unresolved.

## Local documentation verification

- `python3 scripts/test_ci_scope.py`: 7 passed.
- `python3 scripts/check-ai-instructions.py`: eight skills/eight reviewer configurations pass.
- `python3 scripts/check_assurance.py`: integrity passes; all 345 controls remain not assessed.
- `python3 scripts/test_assurance.py`: 27 passed.
- `git diff --cached --check`: passes; six Markdown-only changed paths select docs scope.
- Gitleaks 8.30.1 over the staged repository export: no leaks found.
- Feature-relative Markdown links resolve; ignored `.specify/feature.json` identifies feature021.

These checks validate documentation tooling and integrity only. No build, SQL, browser,
full CI, new native demo, implementation completion or merge is claimed.

## Authorization and next step

The user's next-task request continues the approved roadmap; the later explicit answer
selects continuation under recorded approval after sign-out/MFA expiry with current
account/permission checks. Existing [approval](../../docs/approval.md) and approved04
already require durable bulk jobs. The pending target/lifecycle choice is a material
product decision, not an extra generic permission gate. Preserve the existing pending
question. After its answer, finalize GR-004, run the installed plan/tasks/analyze workflow,
record the concrete approved scope and implement the visible revocation journey with its
required verification. Do not repeat accepted feature020 or renew expired diagnostics.
