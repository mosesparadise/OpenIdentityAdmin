# T018 original-report coverage correction

**Current acceptance:** CI545 supplied all100 genuine build/report manifests and passed the
unchanged floors. Independent archive/step/arithmetic reconciliation is recorded in
[T018 receipts](t018-evidence.json). The diagnosis and pre-execution requirements below
retain the historical correction checkpoint. Final-head checks and reviewed
squash are recorded on PR124.

CI544 ([run42311638811](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/42311638811))
at `c18569efafa56026c3ae3a3370a85bce309a02f7` passed all124 jobs on its first attempt.
Reconciliation retained118 artifact bindings,103 ordinary native TRX reports with5,753
passing executions and no failures/skips,83 UI cases,44 Node contract controls and110
demonstrated behavioral mutation kills. The corrected SQL Server WebKit logout check and
both ordinary Aspire Chromium demos passed. These are useful runtime results, but **T018
is not accepted**: independent coverage reconciliation found lost branch metadata.

## Reproduced defect

`dotnet-coverage18.12.0` merged the same100 original Cobertura reports into outputs with
identical24,065 source-line keys/hit maps (22,553 hit), but different branch populations.
The hosted summary reported2,589/2,698 branches; a local glob merge reported2,415/2,516.
Two repeats with the same explicit hosted input order reported2,509/2,618 and2,559/2,660.
These are real condition omissions, not XML ordering or timestamps. For example,
`DocumentationPolicy.<InvokeAsync>d__20` line38 retains eight branch outcomes in all98
original reports containing that class; one local merge removes them. Hosted merge also
loses conditions from `GrantIntent` line16 that are present in the originals.

Hosted collection used runtime8.0.33; local merge used runtime10.0.14. That difference is
a confound, not a proved cause. A serial public-API merge still loses metadata. A bounded
three-arm native `.coverage` probe exports zero branches where direct Cobertura collection
exports eight, so native-format migration is not a demonstrated correction. No database
experiments were needed for this diagnosis. The [diagnostic receipt](coverage-diagnostic.json) retains input hashes and failed comparisons;
canonical branch equality is not claimed. Earlier merged summary passes cannot substitute
for the corrected gate.

## Correction and trust boundary

The pipeline retains the upstream merge for line-union comparison and inspection. Its
branch summaries are explicitly non-authoritative. `scripts/check_coverage_bounds.py`
uses original per-cell reports, unchanged required module membership and exact90% line/
80% branch floors. It rejects missing/extra/relabelled inputs and binds each report to its
cell, checked-out commit/tree, workflow run/attempt, SDK/configuration/framework, collector
version/package hash/settings and all15 product DLL/PDB hashes. Before/after snapshots
must agree. These are trusted CI build-copy bindings for the existing no-build test
commands, not runtime loaded-module attestation. The manifest scans standard host Debug outputs; it does not attest RID/publish/container
binaries. Current execution follows the reviewed no-build contract.

Branches are keyed by module, class, source file, method name/signature and source line.
Two methods sharing one line remain distinct populations; class condition IDs are not
used across methods. Original class flags/counts must agree with their method union.
Duplicate identities, conflicting denominators/condition shapes, unsupported ambiguous
switch populations and missing branch declarations fail. A missing observation cannot
erase a declaration retained in another report.

For pure jumps, the conservative bound sums each stable condition's maximum observed
zero/one/two covered outcomes. Two50% observations of the same condition remain one
outcome. Switch or mixed points use the greatest observed whole-line covered count;
they do not invent switch outcome identities. Counts remain exact integers and floor
comparisons use rational arithmetic. Only percentage-string validation accommodates
decimal rounding and at most1e-12 percentage-point floating serialization noise.
Branchless required modules remain explicit. Instrumented test drivers outside the
existing required-module policy gain no new floor or exclusion.

## Verification and remaining acceptance

All65 independent synthetic controls pass (29 arithmetic/class-method and36 provenance/CLI).
The correction includes controls for arithmetic, identity loss,
class/method disagreement, malformed declarations, exact floors and provenance/cell
substitution. Saved CI544 reports provide feasibility evidence only: they predate the
new build manifests and cannot receive retrospective provenance. All15 modules meet the
unchanged floors with original method bounds; AuthServer is5,079/6,328 (80.2623%).

New current-head CI must collect genuine manifests and pass the corrected gate, all
ordinary tests and required reviews before T018 can close. PR124 remains draft/unmerged.
The initial pilot replay/late-issuance window, all six historical findings and manual/
Windows/Firefox/A-009/vendor/production/public-release gates remain open; refresh is disabled.
