# Code quality and repository conventions

The user's 10 October 2026 request authorizes this update to PR #2. The supplied EditorConfig (RehanSaeed/EditorConfig 4.0.0, dated 2021) and Visual Studio gitignore are references. Applicable conventions are adapted to the current .NET 10/Aspire/Spec Kit repository; comments in uploaded files do not authorize unrelated work. The EditorConfig's MIT attribution is retained.

## Enforced rules

- Analysis is pinned to .NET 10 recommended rules in Directory.Build.props and the SDK selected by global.json. Built-in analyzers and selected additional security diagnostics cover all handwritten C# projects, including tests; warnings are errors and selected code-style/naming rules run during builds.
- Verify C# formatting in CI with the same SDK and checked-in EditorConfig. Fix existing violations before enabling the gate. Generated EF migrations/SDK output remain explicitly identified; handwritten migration hosts, configuration and tests are included.
- The static-analysis CI job proves enforcement with clean, weak-hash (CA5350), formatting (IDE0055) and malformed-name (IDE1006) controls, followed by a restored clean build. Temporary probes inherit the real repository configuration and are removed. No blanket analyzer suppression or analysis-disabled build is accepted.
- Keep applicable common text, project/XML/JSON/YAML, web, shell and Markdown conventions. Prefer file-scoped namespaces, existing outside-namespace imports, explicit braces and ordinary .NET naming. Retain readable underscore-separated test scenario names.
- Ignore machine-specific IDE state, binaries, profiling/test artifacts, local secrets, database files and package outputs. Preserve source, generated EF migrations, lock files, Spec Kit definitions and AI instruction files. Ignore files are not a substitute for secret scanning.
- Record narrow diagnostic exceptions with reasons. No claim that static checks replace boundary tests, independent assessment or certification.

## Delivery policy

Squash each pull request into one coherent target-branch commit. Update the PR title/body for its final scope and retain verification links. Intermediate review commits need not be force-pushed away. Never bypass protections or impersonate another reviewer when GitHub disallows author self-approval.

The living [roadmap](../roadmap.md) links detailed feature task lists and marks completed, partial, planned and externally blocked work separately. Update it with each delivered increment; leave the approved v0.2 snapshot unchanged.

## Local and CI commands

```sh
dotnet restore --locked-mode
python3 scripts/check-static-analysis.py --self-test
```

The script builds with analyzers, verifies whitespace and verifies unused imports, then exercises the optional self-tests. CI always supplies `--self-test`. It fails on nonzero results, missing expected defect diagnostics or timeout. These three intentional negative controls are separate from the required .NET matrix in `eng/test-matrix.json` and the Python gate regressions.

Unused imports (IDE0005) are checked with `dotnet format style --severity info --diagnostics IDE0005 --verify-no-changes`: Roslyn build enforcement otherwise requires XML documentation generation. The other selected rules, including IDE1006 naming, are enforced by builds. Web/TypeScript conventions are editor settings until the frontend is implemented; its lint/type/contract/browser gates belong to its feature, not an unexecuted claim here.

## Adaptations and exceptions

- Kept outside-namespace imports to match the existing file-scoped C# layout; omitted obsolete/unused third-party analyzer settings and the unrelated domain-specific VOG038 instruction from the attachment.
- Retained common UTF-8/LF conventions, two-space structured/web files, Markdown hard breaks and the reference's applicable .NET naming/bracing preferences. Generated EF files are scoped to the two migration assemblies' Platform/Sessions directories; handwritten migrator logic stays included.
- CA1707 is disabled only for test scenario names. Existing ASPIRE010 suppression remains limited to the AppHost's documented publish limitation. No security rule was suppressed to make this update pass.
- Cached constant test arrays, invariant numeric conversion/ordinal comparisons and matching overridden parameter names resolve recommended diagnostics. Cached migrator logging delegates use event IDs 1001/1002 and preserve fixed messages plus exception type only; the exception argument is null.
- Ignore checks verify local keys/IDE/build/test outputs are excluded while lock files, native migrations, Spec Kit files and AI configurations remain trackable. Public PEM test certificates can be deliberately admitted by a reviewed specific exception if a later feature needs them.

See [quality verification](../../specs/002-issuer-foundation/verification-quality.md) and the PR's latest checks for actual execution evidence. CI enforcement does not claim that GitHub branch-protection settings were changed.

## Pull request titles and squash commits

Use Conventional Commits for every PR title: `type(scope): imperative description`, with an optional scope and `!` only for an actual breaking change documented in the body. Choose the type for the final change (`feat`, `fix`, `test`, `docs`, `refactor`, `perf`, `build`, `ci` or `chore`). For example: `feat(auth): add first-party login` or `test(auth): verify bootstrap conflicts and rollback`. Update the title when scope changes, and use that title for the squash commit. Do not rewrite already merged history to retrofit this convention.

## Principal-engineer standard (12 October 2026)

Constitution 1.1.0 adds the user's maintainability obligations. New and changed code must comply;
existing debt is corrected in separate reviewed PRs alongside MFA work. The tracked adoption and
retrofit tasks are [feature 009](../../specs/009-engineering-standards/tasks.md). This policy does
not claim that every existing method has already been documented or every test renamed.

- Prefer one clear responsibility per method/class. Review thresholds are 40 logical code lines per
  method and 300 per class. Extract meaningful operations; do not split into arbitrary wrappers or
  partial declarations to hide complexity. Reuse stable mechanisms across projects while preserving
  feature ownership. Avoid speculative abstractions and global constants/service collections.
- Use `Given_<condition>_When_<action>_Then_<outcome>` for C# test methods. Python unittest methods
  use `test_Given_..._When_..._Then_...`; Node test descriptions use readable `Given … When … Then …`.
  Preserve independent assertions and update exact discovery inventories, filters, mutation selectors
  and current requirement references together. Do not rewrite historical evidence; retain a rename map.
- Replace unexplained strings/integers with framework constants, purpose-named feature constants,
  value types or validated deployment options. Obvious intrinsic values such as zero, one and empty
  strings are allowed. Test expectations and negative inputs must be independently authored; named
  test constants must not import the production values they are intended to verify.
- Document every handwritten method's purpose and contract using XML, including meaningful parameter,
  return, failure/cancellation and ownership details where applicable. Public reusable classes and
  externally consumed methods also need detailed safe usage examples. Examples must not contain real
  identities, tokens or credentials. Test methods need a meaningful summary, not example boilerplate.
  C# rejects XML documentation on local functions (CS1587); document local functions with ordinary
  purpose/contract comments. Named methods remain preferable when an operation merits independent reuse.
- OpenAPI presentation metadata comes from validated appsettings-backed options. Supported API versions,
  document identity, protocol routes, authorization and renderer security invariants stay code-owned.
  An arbitrary configuration switch must not enable production documentation before its security gates.

## Syntax-aware gate and legacy debt

The approved tool lives at `eng/IdentityPlatform.CodeQuality`, in the **Engineering** solution folder.
It uses centrally pinned Roslyn 5.0.0 and has no production dependency edge. It is verified with seeded
rule/evidence controls; it is not silently included in the nine product-module coverage claims.

| Rule | Automated check |
| --- | --- |
| CQ001 | Fact/theory and public attributed test scenario names contain nonempty Given/When/Then components. |
| CQ002 | Handwritten methods/constructors/operators/accessors have parseable nonempty XML summaries; local functions have purpose comments. |
| CQ003 | Externally visible/overridable methods/types have nonempty summaries and examples; test projects are excluded from the example requirement. |
| CQ004 | Method-sized declarations exceed 40 code-bearing lines. |
| CQ005 | Type declarations exceed 300 code-bearing lines. |
| CQ006 | Unnamed numeric/string/interpolated-text candidates outside deliberate named-value, attribute-data and expected-test-value contexts. |
| CQ007 | Conditional source requires explicit review of branches that an ordinary syntax parse may not select. |
| CQ008 | Partial declarations require explicit cohesion/generator review of their combined size across files; splitting a large class cannot silently evade review. |

Size counts distinct token-bearing source lines excluding brace-only lines and comments; multiline
string contents still count. Syntax fingerprints retain formatting and documentation so a changed
member cannot reuse an unchanged-code allowance. Normal formatting checks remain required. The gate
checks syntactic structure, not semantic explanation quality: reviewers must assess meaningful names,
complete/accurate contracts, useful examples, reusable boundaries and independent test oracles.

Only native EF artifacts with matching class/name/base/method shapes in the provider assemblies' exact `Platform`/`Sessions` directories are excluded.
The handwritten Migrator is included. Build output and dependency directories are excluded; adding a
`GeneratedCode` attribute to handwritten code does not suppress this tool.

`eng/code-quality/allowances.json` records exact findings, owner and remediation task. Initial legacy
entries must also exist when scanning trusted `main` source (`38535bd`) with the same rules. Subsequent
legacy inventories are removal-only against the trusted PR base. No automatic refresh occurs in CI.
Changed findings must be corrected or receive a separately reviewed exact exception with a rationale,
owner, task and expiry. Expired, duplicated or stale allowances fail validation. Fixing a finding
requires removing its old allowance. Exception metadata is a review record, not proof of human approval.

Initial inventory: 1,984 rule findings (not distinct defects): 237 scenario-name findings assigned to
T013, 409 production findings to T014, and 1,338 fixture/test findings to T015. The exact inventory is
a starting debt record; it must shrink through reviewed remediation. Language-specific enforcement
beyond the C# tool is tracked in T016. New Python/Node code follows the same intent under review.

After building the solution:

```sh
python3 scripts/test_code_quality.py
python3 scripts/check_code_quality.py
```

The normal static-analysis command runs these checks. CI supplies the trusted base commit through
`IDENTITY_QUALITY_BASE`; local checks use the merge base with `origin/main`. The tool's `--inventory`
command reports safe finding metadata only and never writes acceptance allowances.

## Documentation-only CI

Skip expensive suites only for an entirely eligible prose change set. The selector examines the
complete PR base-to-head diff (or push before-to-after diff), including both sides of renames and file modes; executable or symlink prose never qualifies. A final
prose commit on a PR that still contains code changes therefore retains full verification. Reusing
results from another source revision is not part of this optimization.

Root README/CONTRIBUTING/CHANGELOG and Markdown under docs/specs can qualify. Engineering policy,
approval/amendment snapshots, AI rules, Spec Kit instructions, workflows, scripts, source, tests,
configuration, dependencies and unknown paths require full checks. Manual/nightly verification always
runs the full obligations. Missing/invalid/empty diff evidence also selects full verification.

Every run retains change-selection controls, instruction checks, assurance checks, source secret
scanning and the final **Verification acceptance** job. Documentation-only application jobs are
explicitly unselected, not credited as passing tests or coverage. The final gate rejects any unexpected
skip, failure, cancellation, missing job or unsupported scope. Workflow-level path ignores and commit
skip markers are not used. Configure branch protection to require this final check through a separately
authorized repository-administration change if needed; this feature does not claim to configure it.
