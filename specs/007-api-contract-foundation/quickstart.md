# Validation quickstart

This is contract tooling, not a production administrator UI. Actual Admin.Api/Admin.Bff hosts still expose health only: `/docs`, `/openapi` and fixture routes return404 in every tested environment. Do not enable documentation until the M02/M04 security dependencies are implemented and accepted.

## Linux / WSL2 core checks

Use SDK10.0.401, Node24.19.0 and npm11.9.0, plus `strace`. Run from the repository root:

```bash
dotnet tool restore
dotnet restore --locked-mode
npm ci --prefix src/frontend/IdentityPlatform.Admin.Contracts --ignore-scripts --strict-peer-deps
npm run bundle --prefix src/frontend/IdentityPlatform.Admin.Contracts
dotnet build --no-restore --nologo
python3 scripts/test_contract_evidence.py
python3 scripts/test_contract_supply_chain.py
python3 scripts/verify_contracts.py core
```

The core verifier discovers exact MTP identities; runs native generation/version/header tests; exports both actual entry points twice under a socket-effect audit; validates the pinned diff tool; checks npm/renderer provenance; validates all four documents; compares the exact supported Git baseline; rejects generated contract/type drift; and runs the Node compatibility controls. First-baseline initialization is allowed only against immutable pre-feature commit `0c1de1e8b5dbee5f53d0efff7814fdc03dfd6e93`. CI provides the exact PR base SHA through `IDENTITY_CONTRACT_BASE`; local verification defaults to `origin/main`. Do not substitute the candidate head for a PR's base.

After intentionally changing contracts, regenerate with `npm run generate --prefix src/frontend/IdentityPlatform.Admin.Contracts`, review the diff, and rerun the verifier. Regeneration does not approve a breaking change. The `generated/` directory contains actual host inventories (currently no management operations); `tests/generated/` contains explicitly synthetic representative operations. Only the latter drives the test consumer.

## Browser and full acceptance

Provision trusted HTTPS and the pinned Chromium/Firefox/WebKit binaries using the existing [verification workflow](../../.github/workflows/verify.yml), then run `python3 scripts/verify_contracts.py chromium` (repeat for `firefox` and `webkit`). Do not disable TLS validation. The fixtures start and dispose their own local HTTPS servers and use synthetic cookies; they do not demonstrate real administrator authentication. Reports and viewer captures go under `artifacts/tests/contracts-*` and `artifacts/contracts/`.

The GitHub Actions contract jobs isolate these checks from issuer execution deadlines. Browser downloads are denied by the current managed workspace network policy, so browser acceptance is obtained in Actions, not claimed from local execution. Both-provider ready-host404 checks run in the existing Aspire matrix. Full acceptance also requires every existing static, supply-chain, provider, browser, lifecycle, mutation and coverage job, plus exact-head review and normal squash merge.

Bundled renderer component inventory/advisory/notice limitations are recorded separately from the npm toolchain; neither a successful wrapper license check nor zero npm audit findings is complete vendor assurance. Full authenticated documentation/proxy and real React/resource-contract acceptance remain M02/M04 dependencies.
