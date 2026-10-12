# Implementation Plan: Engineering standards

## Summary
Deliver enforcement and proportional CI before resuming MFA; keep legacy cleanup in separate coherent PRs. User-approved structure and scope are recorded in spec.md.

## Technical Context
.NET10 pinned SDK; Roslyn5.0.0 already in the resolved tool graph, direct tooling reference centrally pinned; Python existing CI selection; xUnitv3/MTP unchanged. Engineering console tool with small Features-based analysis/reporting components. No runtime/tool dependency edge.

## Constitution Check
Principles19–23 implement the explicit user request;1–18 retained. Security constants remain code-owned. Source-generated EF is excluded narrowly; handwritten migrator included. Changed-code enforcement cannot claim existing debt is fixed. Independent architecture/test/API review required. Test expectations remain independent.

## Project Structure
eng/IdentityPlatform.CodeQuality/{Features/Analysis,Features/Evidence,Features/Verification}; grouped under Engineering in IdentityPlatform.slnx. Existing scripts own CI selection and orchestration. eng/code-quality stores reviewed legacy inventory and scoped exceptions. Existing backend feature folders own OpenAPI options; no new production project. Production Programs remain thin.

## Design
Fail closed on unsupported/missing change evidence; allow only prose paths, not code hidden by extension-based broad ignores. Include both old/new rename paths. Manual/nightly always full. Do not use workflow-level paths-ignore or skip commit markers because required checks can remain pending. Always run lightweight assurance/instruction/secret checks and a final result gate. Full path retains all current test and coverage requirements.

Quality findings bind rule/path/member signature and syntax fingerprint; no source literals in diagnostic output. Legacy allowance is exact and removal-only after adoption, not a wildcard suppression. New/changed findings require fixes or an explicit reviewed exception with owner/reason/expiry. Rule limitations are documented; semantic clarity/reuse remains independent review work. Logical size counts nontrivia token-bearing lines excluding brace-only lines; fingerprint includes measured size to prevent whitespace gaming. Data literals are allowed only in named declarations, obvious intrinsic values, attribute data and independent expected-test inputs; no shared production/test oracle shortcut.

OpenAPI settings hold descriptive metadata; version/route inventory and renderer security constraints remain code-owned. Fail on missing/invalid configuration. Preserve authoritative generation, asset handling, no-store/CSP and production docs gate. Add regression that proves hostile fixture metadata reaches the served document.

## Delivery
T001–T009: rules/CI/quality adoption PR. T010–T012: configured OpenAPI and renderer-regression PR. T013–T016: explicit ongoing legacy retrofit groups alongside MFA. Each PR independently reviewed/verified/squashed; no feature-wide completion claim while retrofit remains.
