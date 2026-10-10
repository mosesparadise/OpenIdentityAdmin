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
