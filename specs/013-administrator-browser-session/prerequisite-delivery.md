# B3 identity and transaction prerequisite evidence

22 October 2026. [PR #71](https://github.com/mosesparadise/OpenIdentityAdmin/pull/71) implements strict native-accepted identity/intent projection and shared Sessions ticket transaction participation. This is partial T003/T007 delivery, not a completed B3 browser lifecycle or production activation.

## Exact source and execution

| Boundary | Verified identity |
| --- | --- |
| Source | `53c78662dc9db8a90d1dd1352392f9855178183e` |
| Base | `f0f5460c482029286dd0113be0f5a1cba37a691e` |
| Tested merge | `1417b318b832e7b987ad2dfe4c4185064f62c8b4` |
| Source/tested tree | `896f1da4352c1eb355971cc0979f3c83944f23db` |
| Corrected workflow | [39870793377](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39870793377) |

**Delivered:** PR #71 was normally squash-merged as `017bfaa5396e6d2931c99af110faa0710c60e7bb` after all 67 exact-head checks and independent architecture/test/security/evidence reviews passed. Source, tested merge and squash share tree `896f1da4352c1eb355971cc0979f3c83944f23db`. Main and PR head/reviews were rechecked immediately before merge; no review restriction was bypassed.

## Implemented and verified boundaries

[Identity/state implementation](native-integration.md) contributes 66 Host controls for original signed-field shape, protected intent, source expiry, fresh password/MFA claim validation and canonical two-claim projection. [Transaction participation](native-transactions.md) preserves B1 owning wrappers while allowing additional Sessions writes before one guarded commit. It captures immutable lifetime bounds, permanently rejects failed or overlapping operations, binds the exact native transaction and prevents retry after an uncertain commit.

Rebuilt local Host execution passes 1,192/1,192, AppHost unit execution passes 65/65, and all 211 Python controls pass. Builds, formatting/style, source-secret checks, code quality and the exact dependency/advisory gate pass. No deadline, assertion, module floor or provider interface was weakened.

At final source `53c78662dc9db8a90d1dd1352392f9855178183e`, both actual provider jobs pass 8 Persistence, 8 AppHost Foundation and 10 BffSessions cases each. The BffSessions evidence includes all six new transaction cases and fourteen preserved cases, zero failures/skips. All five existing session test source files are byte-identical to the base. Native tests prove shared ticket/audit/mapped-marker commit and rollback, later owner failure, caught audit failure, missed CAS, cancellation, immutable final expiry, transaction ownership, both rejected-overlap interleavings and uncertain native commit. They do not claim browser-generation race completion or B2 durable redemption through the marker.

| Provider | Job | Retained artifact | Artifact SHA-256 |
| --- | --- | --- | --- |
| PostgreSQL | `120144901827` | `12466917272` | `9f4e204b4f93e5251f5e30e97aa57c667c314f46b383a2a6a1eba6969612f889` |
| SQL Server | `120144901594` | `12465989523` | `e57a68db7d4ac1d5eaeb74974065fea30c88cc6032b36f60860c2cb796d2e8d0` |

Independent architecture/test and security reviews pass after correcting rejected-overlap resurrection and preserving primary test failures during cleanup. The provider evidence reviewer independently bound checkout, source/tested trees, finite native logs and the exact-head verification code. That code requires disjoint exhaustive native discovery and matching TRX identities, Passed outcomes, zero skips/errors/warnings and consistent unique definition/entry/execution bindings before emitting its verified receipt.

**Evidence limit:** independent raw XML inspection was unavailable: the artifact connector returned File Service references, and their download URL received proxy CONNECT 403 from this workspace. The provider verdict relies on CI's own exact-case/TRX reconciliation, reviewed source and finite logs, not a claim that the reviewer downloaded and inspected raw XML. Artifacts remain retained in the workflow.

## Security mutation evidence

Independent review at final source `53c78662dc9db8a90d1dd1352392f9855178183e` reconciles all eight mutation jobs with the unchanged manifests: 7 issuer, 8 administrator, 6 governance and 3 protocol targets per provider, totaling 48 behavioral kills. All 48 baseline and 48 restored controls pass, and source hashes are restored. Review binds exact checkout, finite native logs, upload digests and artifact metadata to the verifier that rejects unrelated errors or timeouts as kills. The raw artifact transfer limitation above also applies here. Prior successful mutation evidence at `c861d845` remains in its retained run; the table below records fresh final-source executions.

| Suite | Provider | Job | Artifact | SHA-256 |
| --- | --- | --- | --- | --- |
| Administrator mutations | PostgreSql | `120144900734` | `12465828999` | `c8fe8da50f600a8a12e0c0c5363fc1a87ccd07e1133d2f1feecedfd57cc9582d` |
| Critical mutations | SqlServer | `120144900770` | `12466696596` | `9e0f62abaf4ab3d27dace65120aec2f03f83a04320011d4c426e8fecf7275645` |
| Administrator protocol mutations | SqlServer | `120144900850` | `12466038676` | `c4e3a4f08f0873409f100d6995c0ab616ef077aebbb4ef004568c4988c9a7c74` |
| Governance mutations | SqlServer | `120144900854` | `12466626396` | `a1c0e36922f9d0dc309bc477c04ac03c838bfe9560e0997dfc5abb4d0c9b2904` |
| Administrator protocol mutations | PostgreSql | `120144900895` | `12466351620` | `322edee7d0e87d59e20041dad84ec7e12b6653a24653fa799598b2558ab8bc53` |
| Governance mutations | PostgreSql | `120144900901` | `12466336940` | `e8646d0b2710c04222a4aab89e66db5f0d79edb3848870a1f6b273878c1ca371` |
| Critical mutations | PostgreSql | `120144900927` | `12467091266` | `bc7eddbe701fc922ccc34775b688c562c47574c4bc68be80f65feb29048e336f` |
| Administrator mutations | SqlServer | `120144900618` | `12466601904` | `b1a3ead7c920606c2a7a59eff25a7af730a98b2c8eeea32f44082c05c8e76028` |

## Preserved failures

Initial source `8511df7` failed static analysis on an unused test import and Contracts core on new/changed viewer advisories. Both checks pass on corrected source. The [bounded advisory reassessment](../../eng/contracts/vendor/configuration-review-2026-10-22.json) retains unresolved vendor inventory, qs, notice and activation/public-release gates; no patched embedded DOMPurify claim is made.

Corrected attempt 1 ended with 63 successful jobs, two WebKit provisioning cancellations, failed acceptance and skipped aggregate coverage. Contracts job `120118222262` and administrator Accounts/PostgreSQL/WebKit job `120118222567` exhausted their existing 15/20-minute job deadlines while apt downloaded Ubuntu libraries from `azure.archive.ubuntu.com`, before browser tests began. Missing evidence is not a pass or a product test failure. The single targeted failed-jobs retry passed PostgreSQL/WebKit Accounts 8/8 but repeated the Contracts WebKit pre-test provisioning cancellation. Successful provider jobs were not rerun. Preserve the original cancellations regardless of the retry outcome; no test timeout, assertion or retry policy was changed.

[The provisioning correction](browser-provisioning.md) records the failed retry and the verified no-op at source `746ec62fcf2b65a34e74841a902931c85800ff7f`, run `39868529903`: the hosted runner uses `mirror+file:/etc/apt/apt-mirrors.txt`, so direct regional URI replacement changed zero files. The corrected helper recognizes that exact selector and uses the official HTTPS archive. Seven synthetic controls and independent architecture/test/security review pass. Native product/test source is unchanged. At final source, contrast job `120144900838` reports one source file replaced, then downloads Ubuntu dependencies from `https://archive.ubuntu.com/ubuntu`; there are no Azure archive download lines. It installs `libflite1` and completes the experiment successfully. Independent review confirms this actual source replacement and provisioning, without claiming the remote mirror's underlying cause is known. All 67 final-source jobs pass. Coverage job `120151237330` reconciles all 50 required inputs and passes all 15 unchanged module floors; artifact `12466783459` has SHA-256 `c0c58d1a67e407a71b09bc780641dce4cde11bddadaabc4cfc9292353ea8b9cd`. Final acceptance job `120151426994` passes. Source acceptance does not close any historical browser finding or external gate.

## Remaining roadmap work and gates

Next prove the pinned native end-session path against persisted fixed-client metadata on both providers, including optional ID-token-hint handling, exact Web application return-URI comparison, endpoint permission and untrusted `logout_hint`. Existing login-only publication/callback guards must remain intact; product logout needs separate confirmation and committed-sign-out handling. The audited old/new registration upgrade must preserve disabled state without relaxing B2 initialization or live admission.

Then complete issuer continuation/PAR binding, durable generation/intent/actor-audit orchestration, fresh password/MFA product sign-in and local plus issuer-assisted logout. T003/T005–T024 are not completed by this prerequisite. No cookie, API, refresh, React or production/public-release activation follows.

[A-009](../../docs/amendments/A-009-roadmap-browser-deferral.md) remains the active internal Playwright/Firefox disposition; original deferred tests and failed evidence remain preserved. PR #69 remains draft/unmerged and #70 tracks the report feature request. All six historical findings, 48 human rows and 345 not-assessed controls remain open. A-004 remains expired; A-005/A-006 concluded.
