# OpenIdentityAdmin

A greenfield administration platform built with OpenIddict and ASP.NET Core Identity. The approved product includes client/resource and user/role management, security, auditing and operational health through a React/TypeScript Admin BFF and a Razor Pages authentication service.

**Current state: foundation verified; product features remain in development.** This branch contains native persistence, provider migrations, safe configuration/health, Aspire orchestration, test/coverage infrastructure and approved engineering instructions. Hosts currently expose operational health only. Login/token issuance, the usable BFF, management APIs, React/Razor themes and Scalar/Swagger are later approved slices, not completed capabilities. This is not a deployable or certified identity product.

## Approved design

- [Approval and amendment record](docs/approval.md)
- [Detailed PRD](docs/approved-v0.2/00-prd.md)
- [Architecture and decisions](docs/approved-v0.2/02-architecture.md)
- [Documentation index](docs/README.md)
- [Foundation specification](specs/001-platform-foundation/spec.md), [tasks](specs/001-platform-foundation/tasks.md) and [actual verification](specs/001-platform-foundation/verification.md)
- [Accepted certification sequencing amendment A-001](docs/amendments/A-001-certification-sequencing.md)

Version 0.2 and A-001 are approved. The ten-document baseline preserves its original snapshot wording; the approval record supersedes its former awaiting-approval status. Mandatory S256 PKCE remains in the approved implementation plan. The Basic OP suite compatibility dependency does not block implementation under A-001, but actual Basic OP/Config OP certification remains a public-release gate. There is no Duende migration scope.

## Project organization

Source and solution folders are grouped under `aspire`, `backend`, `core`, `migrations`, `persistence` and the reserved `frontend` area. Tests use corresponding responsibility groups plus `architecture`; Features remain inside each project. See the [layout and native MTP guide](docs/engineering/project-layout.md).

## Local foundation

Prerequisites: the .NET SDK pinned in `global.json`, Python 3.11+, Docker-compatible containers, access to NuGet/container registries, and a locally trusted ASP.NET HTTPS development certificate. Database images are pinned by digest. SQL Server needs an appropriate x64 container host. The AppHost is explicitly Development-only and uses disposable storage; stopping it loses local data.

```sh
dotnet tool restore
dotnet restore --locked-mode
dotnet build --no-restore
dotnet dev-certs https --trust
dotnet run --project src/aspire/IdentityPlatform.AppHost -- --Database:Provider=PostgreSql
# Or select the other required production provider:
dotnet run --project src/aspire/IdentityPlatform.AppHost -- --Database:Provider=SqlServer
```

The dashboard uses `https://localhost:17100`. Use its generated authenticated login link printed locally. Each application receives a dynamically allocated HTTPS endpoint. Only `/alive` and `/health` are currently available. On Linux, follow the dev-certificate tool's `SSL_CERT_DIR` guidance while preserving the system trust directory; see the [quickstart](specs/001-platform-foundation/quickstart.md).

The dedicated migrator initializes the two stores and provisions separate runtime principals for disposable local development. Runtime hosts do not receive migration credentials. Admin BFF receives only session-store credentials. Production keys, database principals and hosting are not created by this AppHost.

## Verification

```sh
python3 scripts/check-ai-instructions.py
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql
python3 scripts/verify.py SqlServer
```

All five test projects use xUnit v3 (`xunit.v3.mtp-v2` 4.0.2) with native Microsoft Testing Platform 2.5.1. Build first: these scripts deliberately use `--no-build`/`--no-restore`. Each provider profile includes native persistence and actual Aspire process tests. An unavailable required provider fails; it is never substituted or skipped. The GitHub workflow runs all three profiles and merges child-process coverage, enforcing 90% lines/80% branches for each handwritten module. No overall average can conceal an untested host. CI also checks current NuGet advisories, locked license evidence and the source snapshot for secrets; see [supply-chain evidence](docs/engineering/supply-chain.md). See `eng/coverage-policy.json` for generated-code exclusions.

[CI run 38040891293](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/38040891293) verified the xUnit v3/MTP and grouped-layout implementation: **92 .NET tests** across unit/PostgreSQL/SQL Server and **19 verification-script regressions** passed, with no skipped required cases. All nine handwritten modules passed their coverage floors, including child hosts; dependency, license and secret checks passed. The managed local environment blocks Microsoft's redirected SQL image hostname, so real SQL Server acceptance is established by GitHub CI. See the [current verification record](specs/001-platform-foundation/verification-mtp-layout.md) for the exact commit, artifact references, coverage and review dispositions.

## Implementation workflow

The repository includes the genuine GitHub Spec Kit 1.1.3 Codex skills integration and ratified constitution. Use `$speckit-specify`, `$speckit-plan`, `$speckit-tasks`, `$speckit-analyze`, `$speckit-implement` and `$speckit-converge` in Codex as applicable; do not reinitialize over the checked-in artifacts. Tracked feature specifications, plans and tasks live under [`specs/`](specs/); the foundation is in [`specs/001-platform-foundation/`](specs/001-platform-foundation/). `.specify/feature.json` is generated, ignored, per-checkout state and is absent from a fresh clone. Select an existing feature before running its Spec Kit commands; this prerequisite command also creates the local pointer:

```sh
SPECIFY_FEATURE_DIRECTORY="$PWD/specs/001-platform-foundation" \
  .specify/scripts/bash/check-prerequisites.sh --json --require-tasks --include-tasks
```

Features live within their owning projects. Composition roots and generated migrations are explicit exceptions. Protocol application decisions will use Minimal APIs while OpenIddict owns validation and cryptography. Eight specialized skills and reviewer configurations complement Spec Kit; [their evaluation](docs/engineering/ai-evaluation.md) is bounded evidence, not security or certification assurance.
