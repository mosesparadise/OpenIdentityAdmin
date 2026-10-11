# Validation quickstart

Use this for the automated controlled login pilot. For Windows, first follow the [WSL2 instructions](../../docs/operations/first-party-pilot.md#windows-with-wsl2). Prerequisites are the pinned .NET SDK/tools, Python 3.11+, Docker with both supported provider images, explicitly installed pinned browsers and trusted development HTTPS. Never disable certificate checks or substitute a provider/browser. Accepted execution and remaining gaps are in the [current matrix](acceptance-matrix.md); these instructions are not themselves execution evidence.

## Build and provision trust

```sh
dotnet tool restore
dotnet restore --locked-mode
dotnet build --no-restore --nologo
dotnet dev-certs https --trust
dotnet run --project tests/tooling/IdentityPlatform.BrowserInstaller --no-build -- install --with-deps chromium
```

Follow `.github/workflows/verify.yml` for the canonical Ubuntu 24.04 trust setup. The public synthetic development certificate is exported without a private key and installed in the OS/OpenSSL bundle; Chromium imports that exact certificate into NSS as a trusted peer. For Firefox, `IDENTITY_TEST_CERTIFICATE` points to the public PEM so the fixture imports only that certificate into its private disposable profile. Preserve system roots when setting `SSL_CERT_DIR`. The test client uses `127.0.0.1` and issuer uses `localhost`; the certificate must cover both. Browser downloads happen explicitly before discovery/tests, never inside them. Do not upload profiles, traces, HAR or storage snapshots.

## Execute genuine login and denial journeys

```sh
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql
python3 scripts/verify.py SqlServer
python3 scripts/verify.py PostgreSql --browser chromium
python3 scripts/verify.py SqlServer --browser chromium
```

The provider profiles run HTTP/persistence/real-process tests. Browser profiles drive the actual protected Razor form, callback and independent token validation; no authentication shortcut is used. Fixtures create their own synthetic credentials and HTTPS relying party, keep state/nonce/verifier/tokens on its server and dispose resources/private files afterward. There is no separate test-application URL to visit after starting AppHost.

For the full six-cell browser matrix (required on relevant changes, nightly/manual acceptance and the T030 checkpoint):

```sh
dotnet run --project tests/tooling/IdentityPlatform.BrowserInstaller --no-build -- install --with-deps firefox webkit
python3 scripts/verify.py PostgreSql --browser firefox
python3 scripts/verify.py SqlServer --browser firefox
python3 scripts/verify.py PostgreSql --browser webkit
python3 scripts/verify.py SqlServer --browser webkit
python3 scripts/verify_contrast_experiment.py
```

Chromium remains required on both providers for every PR. `scripts/browser_matrix.py` determines additional CI cells; unknown change evidence selects all engines. The focused WebKit runner checks 60 fresh observations separately from ordinary case counts and coverage. Missing prerequisites or skipped required cases fail acceptance.

## Remaining gates and evidence

Run the normal static-analysis, assurance and supply-chain gates; mirror `.github/workflows/verify.yml` for child-process coverage collection/merging across **every selected** partition. Preserve all nine per-module90% line/80% branch floors. Selected browser counts vary by change scope; do not copy the historical checkpoint total onto a documentation-only run without inspecting its selected matrix and reports.

Critical mutation verification deliberately changes source. Use a **separate clean disposable checkout for each provider**, restore/build there, and never run it in the working checkout holding local changes:

```sh
# In the corresponding clean disposable checkout only:
python3 scripts/verify_mutations.py PostgreSql --disposable-checkout
# In a separate clean disposable checkout:
python3 scripts/verify_mutations.py SqlServer --disposable-checkout
```

The runner requires passing baseline/restored controls, exact test identities and demonstrated behavioral failures; a build error or skipped case is not a killed mutant. Keep only sanitized reports/reviewed empty-page screenshots. [Manual/product/release gaps](acceptance-matrix.md#next-work-and-release-gates) remain open even when the currently selected suite passes.

## Optional manual AppHost pilot

Starting the default AppHost supplies only health endpoints. To enable the issuer pilot, follow the [private configuration/initialization runbook](../../docs/operations/first-party-pilot.md#enable-and-initialize) and [issuer protection](../../docs/operations/issuer-protection.md) for distinct externally provisioned credentials and a private key ring. Initialization must finish before AuthServer readiness. A manual browser journey needs a separately configured HTTPS relying party matching the one exact registered callback and the fixed client/S256/Basic profile. `/account/login` is bound to a validated authorization request and is not a direct login entry point. The automated test relying party and future Admin BFF are different components; no usable BFF/React client is shipped yet.
