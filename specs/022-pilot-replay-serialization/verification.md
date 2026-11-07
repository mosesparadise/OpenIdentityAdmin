# Pilot replay correction evidence

## Verified starting checkpoint — 7 November 2026

Remote PR124 is merged with squash `e44667c45d103cdc3d70427577f89b77539f6f22`.
Remote main matches it. Post-merge workflow runs42326203442 and42329191762 report
success. Prior feature021 acceptance remains in its original receipts and PR124;
it is not rerun or relabeled as this correction's evidence.

The previous feature worktree remains clean and preserved. This work uses the new
`129-pilot-replay-serialization` branch/worktree based on current main. No production
source, schema, native test or workflow has changed at this planning checkpoint.

## Review dispositions

Initial architecture/security source review identifies the existing initial replay
window and confirms a preserving-contract correction fits current approval.
It rejects a status-check-only fix and warns against relaxing owned parent/child
transaction invariants. Root review identified manifest-lock self-blocking; follow-up
review confirmed the actual manifest readers and the absence of a supported RCSI
guarantee. The plan now uses isolated coordination state.

Further root/architecture review rejects independent gate and generation commits:
connection loss would leave a late-generation commit race. They now share one
transaction. Native initial replay validation has its own gate transaction; the only
child without reacquisition performs freshly bound early redemption, never replay
cleanup, preventing a late unfenced revoker. Test review required a common old/new
safety oracle, independently seeded historical cleanup state, post-wait freshness/
code-expiry evidence and durable replay followed by response failure. These are now
explicit in C01/C08/C11/C12. Independent architecture/security and test reviewers accept T001 planning only.
Root applied the final wording corrections distinguishing actual transaction release
from later cleanup and independent validation from consumption. No runtime pass is claimed.

Test review also notes that native bulk revocation can change status without changing
its concurrency token. The plan must not infer every intervening revocation is caught
by optimistic concurrency alone: C12 requires actual status, replay denial and absent
credential output. Exact terminal status and usability are separate observations.

## Open acceptance

All C01–C12 runtime cells, fresh/upgrade migrations, exact-source failure/correction
proof, mutation controls and final-head implementation CI remain unexecuted for
feature022. The initial pilot window remains open. Refresh stays disabled. All six
historical findings and Windows/manual/Firefox/A-009/vendor/production/public-release
gates remain open.

## Planning checks actually run

- `python3 scripts/test_ci_scope.py`: 7/7 pass. Changed prose selects the existing
  docs scope; no policy/workflow/source change is included.
- `python3 scripts/check-ai-instructions.py`: eight skill/reviewer configurations pass
  integrity checks; this is not runtime reviewer effectiveness evidence.
- `python3 scripts/check_assurance.py`:345-control integrity passes, all345 remain
  not_assessed; no compliance claim. `python3 scripts/test_assurance.py`:27/27 pass.
- Local structural check:9 unique tasks,12 acceptance cells, no unresolved product
  clarification markers and existing local file-link targets.
- Gitleaks directory scan with redacted output: no leaks. An initial invocation
  referenced an absent optional config and did not scan; the corrected invocation
  used the same default configuration as repository CI.
- `git diff --check`: pass. Spec Kit setup-plan/setup-tasks/prerequisite resolution
  succeeds for feature022; no extension hooks file exists.

T001 is complete for planning. T002–T009 and every runtime acceptance cell remain
open. The planning PR's exact-head documentation CI and merge state are its delivery
record, not implementation verification. Next: retain and run T002's baseline safety
regression, then implement the reviewed persistence/runtime correction.
