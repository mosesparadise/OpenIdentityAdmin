# OpenIdentityAdmin

A greenfield administration platform built with OpenIddict and ASP.NET Core Identity. The approved product includes client/resource and user/role management, security, auditing and operational health through a React/TypeScript Admin BFF and a Razor Pages authentication service.

**Current state: administrator accounts and B3 browser sessions are internally accepted; full product acceptance remains incomplete.** The repository includes extensible relational EF Core providers, PostgreSQL/SQL Server migrations, protected enrollment/MFA/recovery, native administrator protocol flows, server-held BFF sessions and local/issuer-assisted logout. Default startup exposes operational health only. The [Windows Docker Desktop local demo](specs/014-local-b3-demo/quickstart.md) uses the AppHost `local-demo` profile and dashboard initialization with disposable state; no custom PowerShell is required. The [native-entry/package correction](specs/015-aspire-local-entry/verification.md) is delivered through PR #107; actual Windows execution remains open. See its [verification status](specs/014-local-b3-demo/verification.md). Delivered in [PR #116](https://github.com/mosesparadise/OpenIdentityAdmin/pull/116), the [visible React workspace](specs/017-administrator-ui/quickstart.md) adds Overview, session details, appearance, and navigation into the existing fresh sign-in/logout flows; its [acceptance evidence](specs/017-administrator-ui/verification.md) records all 96 successful checks and both-provider/browser acceptance. Server-held refresh and management data/actions remain pending. This is not a deployable or certified identity product.

## Run the visible local workspace

Set **IdentityPlatform.AppHost** as your startup project and select **local-demo**, or run:

```text
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

Use Docker Desktop with Linux containers and a trusted .NET development certificate. Open **https://bff.dev.localhost:17444/** after the dashboard resources become healthy. Initialize the administrator from the dashboard and enroll an authenticator using the [local quickstart](specs/014-local-b3-demo/quickstart.md). The workspace then offers **Sign in**, **Your session**, **Check session** and **Sign out**, with light/dark/system appearance. No custom PowerShell or frontend build command is required. The run is disposable; application management is the next increment.

## Approved design

- [Living roadmap and complete task register](docs/roadmap.md)
- [Current pilot evidence, test coverage and remaining acceptance gaps](specs/004-first-party-login/acceptance-matrix.md)
- [Code quality and squash-merge policy](docs/engineering/code-quality.md)
- [ASVS control register and pending release decisions](docs/assurance/README.md)

- [Approval and amendment record](docs/approval.md)
- [Detailed PRD](docs/approved-v0.2/00-prd.md)
- [Architecture and decisions](docs/approved-v0.2/02-architecture.md)
- [Documentation index](docs/README.md)
- [Foundation specification](specs/001-platform-foundation/spec.md), [tasks](specs/001-platform-foundation/tasks.md) and [actual verification](specs/001-platform-foundation/verification.md)
- [Durable issuer protection specification](specs/002-issuer-foundation/spec.md), [runbook](docs/operations/issuer-protection.md) and [verification](specs/002-issuer-foundation/verification.md)
- [Accepted certification sequencing amendment A-001](docs/amendments/A-001-certification-sequencing.md)

Version 0.2 and A-001 are approved. The ten-document baseline preserves its original snapshot wording; the approval record supersedes its former awaiting-approval status. Mandatory S256 PKCE remains in the approved implementation plan. The Basic OP suite compatibility dependency does not block implementation under A-001, but actual Basic OP/Config OP certification remains a public-release gate. There is no Duende migration scope.

## Project organization

Source and solution folders are grouped under `aspire`, `backend`, `core`, `migrations`, `persistence` and `frontend` (React workspace, generated BFF contracts and shared design tokens). Tests use corresponding responsibility groups plus `architecture`; Features remain inside each project. See the [layout and native MTP guide](docs/engineering/project-layout.md).

## Local foundation

Prerequisites: a stable .NET 10.x SDK accepted by `global.json`, Python 3.11+, Docker-compatible containers, access to NuGet/container registries, and a locally trusted ASP.NET HTTPS development certificate. Database images are pinned by digest. SQL Server needs an appropriate x64 container host. The AppHost is explicitly Development-only and uses disposable storage; stopping it loses local data.

```sh
dotnet tool restore
dotnet restore --locked-mode
dotnet build --no-restore
dotnet dev-certs https --trust
dotnet run --project src/aspire/IdentityPlatform.AppHost -- --Database:Provider=PostgreSql
# Or select the other required production provider:
dotnet run --project src/aspire/IdentityPlatform.AppHost -- --Database:Provider=SqlServer
```

For the new B3 demo from Windows, use [the Aspire AppHost and Docker Desktop Linux containers](specs/014-local-b3-demo/quickstart.md); no WSL terminal is required. The older pilot's [WSL2 instructions](docs/operations/first-party-pilot.md#windows-with-wsl2) still apply to that separate profile. The strict Linux credential adapter is retained inside containers.

The dashboard uses `https://localhost:17100`. Use its generated authenticated login link printed locally. Each application receives a dynamically allocated HTTPS endpoint. The default profile exposes `/alive` and `/health`; the separate Development-only login pilot requires explicit configuration from its [quickstart](specs/004-first-party-login/quickstart.md). On Linux, follow the dev-certificate tool's `SSL_CERT_DIR` guidance while preserving the system trust directory; see the [quickstart](specs/001-platform-foundation/quickstart.md).

The dedicated migrator initializes the two stores and provisions separate runtime principals for disposable local development. Runtime hosts do not receive migration credentials. Admin BFF receives only session-store credentials. Production keys, database principals and hosting are not created by this AppHost. AuthServer can load an explicitly configured durable protection profile; its private Linux storage requirements and wrapping-certificate rollover are in the [protection runbook](docs/operations/issuer-protection.md). Credential-enabled Aspire verification injects synthetic external material; the ordinary local profile remains health-only.

## Verification

```sh
python3 scripts/check-ai-instructions.py
python3 scripts/check-static-analysis.py --self-test
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql
python3 scripts/verify.py SqlServer
# After explicit browser installation and HTTPS trust provisioning:
python3 scripts/verify.py PostgreSql --browser chromium
python3 scripts/verify.py SqlServer --browser chromium
```

The .NET test projects use xUnit v3 (`xunit.v3.mtp-v2` 4.0.2) with native Microsoft Testing Platform 2.5.2. Build first: these scripts deliberately use `--no-build`/`--no-restore`. Each provider profile includes native persistence and actual Aspire process tests. An unavailable required provider fails; it is never substituted or skipped. The provider profiles exclude browser cases. The current internal-development workflow requires retained Chromium/WebKit cells on both providers; Firefox and the exact named WebKit deferrals remain tracked under [A-009](docs/amendments/A-009-roadmap-browser-deferral.md). CI merges every selected cell’s child-process coverage, enforcing 90% lines/80% branches for each handwritten module. No overall average can conceal an untested host. CI also checks current NuGet advisories, locked license evidence and the source snapshot for secrets; see [supply-chain evidence](docs/engineering/supply-chain.md). See `eng/coverage-policy.json` for generated-code exclusions and [native MTP/browser instructions](docs/engineering/project-layout.md#native-mtp) for installation, trust, cadence and exact selection checks.

T032 source `8b8f12ac6095593b392738eb5592a3255ffe3a6d` passed all 17 checks and 1,017 ordinary cases with zero failures/skips in [run 38162030027](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38162030027). Final documentation-head verification and normal squash delivery are recorded on [PR #15](https://github.com/mosesparadise/OpenIdentityAdmin/pull/15); this source checkpoint does not claim its own future merge. The [acceptance matrix](specs/004-first-party-login/acceptance-matrix.md) records the eight-restriction/four-boundary matrix and its limits. T021/full M01-D, qualified manual password-manager/screen-reader/accessibility and timed usability, M01-E/M01-F, Admin BFF/React and the wider R1 product remain open. A-001 retains actual certification as a public-release gate; all 345 ASVS controls remain pending/not_assessed.

Historical foundation, issuer-protection and static-analysis checkpoints remain in their [foundation](specs/001-platform-foundation/verification-mtp-layout.md), [protection](specs/002-issuer-foundation/verification.md) and [quality](specs/002-issuer-foundation/verification-quality.md) records. The managed local environment could not fetch the redirected SQL image or pinned browser downloads, so their accepted execution evidence comes from normal GitHub CI; no provider/browser substitution or relaxed TLS was used.

## Implementation workflow

The repository includes the genuine GitHub Spec Kit 1.1.3 Codex skills integration and ratified constitution. Use `$speckit-specify`, `$speckit-plan`, `$speckit-tasks`, `$speckit-analyze`, `$speckit-implement` and `$speckit-converge` in Codex as applicable; do not reinitialize over the checked-in artifacts. Tracked feature specifications, plans and tasks live under [`specs/`](specs/); the foundation is in [`specs/001-platform-foundation/`](specs/001-platform-foundation/). `.specify/feature.json` is generated, ignored, per-checkout state and is absent from a fresh clone. Select an existing feature before running its Spec Kit commands; this prerequisite command also creates the local pointer:

```sh
SPECIFY_FEATURE_DIRECTORY="$PWD/specs/001-platform-foundation" \
  .specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks
```

Features live within their owning projects. Composition roots and generated migrations are explicit exceptions. The pilot uses Minimal APIs for protocol application decisions while OpenIddict owns validation and cryptography. Eight specialized skills and reviewer configurations complement Spec Kit; [their evaluation](docs/engineering/ai-evaluation.md) is bounded evidence, not security or certification assurance.
