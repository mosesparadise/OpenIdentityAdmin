# B3 verification quickstart

B3 is under internal implementation, with accepted slices through protected local/issuer logout and the initial genuine HTTPS journeys. Read the [current verification map](verification.md), [task list](tasks.md) and [operations handoff](../../docs/operations/administrator-browser-sessions.md) for exact delivered versus candidate status. Earlier planning-only results remain dated in the verification history; they do not describe current execution.

## Resume and verify

1. Verify actual remote main, feature heads, PR/check/review state and local changes. Read AGENTS, approval, roadmap, handoff and referenced specification/evidence before editing. Preserve accepted source, rejected runs, provider plugins, databases and migration histories.
2. Apply the approved A-009 scope: internal B3 work may proceed with Firefox and the two named WebKit certificate cases deferred. Keep them documented and open; do not generalize that deferral to new Chromium/WebKit failures or revive expired diagnostics.
3. Resolve concrete remaining T003/T004 dependencies before dependent routes. Use pinned Spec Kit integration and the current feature artifacts; do not replace repository conventions or invent a test partition.
4. Use real PostgreSQL/SQL Server through the existing Aspire harness, trusted HTTPS, synthetic accounts and disposable protected keys. Fresh password/MFA, native antiforgery, original state and actual commit/cleanup must remain in the path. Do not fabricate positive cookies or ignore TLS failures.
5. Run native discovery before updating mandatory inventories. The authoritative selections live in `eng/test-matrix.json`, `eng/administrator-test-matrix.json` and `.github/workflows/verify.yml`; use `scripts/verify.py` with its actual reviewed partition/story/group arguments. Validate exact disjoint identities and traits, then run focused controls and the required current-head workflow.
6. Review all actual raw reports, coverage inputs/floors, baseline/mutant/restored receipts and screenshots. A timeout or unrelated test failure is not a mutation kill. Axe/palette checks alone did not catch the retained WebKit button clipping, so inspect real screenshots as well.
7. Record source/tested tree/run/review and normal squash only after acceptance. Preserve failures, unproven causation, manual assessment rows and activation/public-release boundaries. T022 documentation does not make absent cleanup scheduling, cross-store audit correlation or restore/fleet automation operational.

## Proportional documentation checks

For eligible prose changes, run `python3 scripts/test_ci_scope.py`, `python3 scripts/check-ai-instructions.py`, `python3 scripts/check_assurance.py`, `python3 scripts/test_assurance.py` and `git diff --check`, and validate relative links and requirement/task references. The fail-closed selector must classify the complete change set; executable, mixed, policy or unknown changes require full verification. The final acceptance job remains mandatory.

Use the pinned Spec Kit 1.1.3 installed skill syntax and existing prerequisite script; no extension hooks are registered in this checkout. Do not alter pinned integration files to rename a working branch. Production enabling, deployment, refresh, API authority, React and public release require their own completed scope and authorization.
