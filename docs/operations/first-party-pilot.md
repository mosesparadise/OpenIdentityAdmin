# First-party issuer pilot

Feature 004 is a Development-only, non-administrator integration pilot. Its [current acceptance matrix](../../specs/004-first-party-login/acceptance-matrix.md) records merged implementation, verified T032 authorization-state acceptance with final delivery on PR #15, and remaining manual/product gaps; the [verification record](../../specs/004-first-party-login/verification.md) retains history. It does not complete production authentication, the administrator BFF, React administration, MFA, consent or certification.

## Windows with WSL2

Run the pilot and its .NET/Docker commands **inside a Linux WSL2 distribution**. The current private credential/key-ring adapter rejects non-Linux platforms. Use a Linux working directory such as `~/work/OpenIdentityAdmin`; keep private PFX/key-ring material outside the repository on the WSL Linux filesystem, not under `/mnt/c`. Follow the protection runbook's directory/file modes (0700 and 0600/0400). Windows ACLs on a mounted drive are not a substitute for that adapter's checks.

Enable Docker Desktop's Linux engine and integration for that distribution, or use a supported Docker engine in WSL2. Confirm `docker info` from the same WSL shell. SQL Server's pinned image requires an appropriate x64 container host and adequate memory. Install the SDK selected by `global.json`, Python 3.11+ and the explicit pinned browser dependencies in WSL. First run the default health-only AppHost from the [README](../../README.md#local-foundation); it requires no pilot account or issuer-key provisioning. Stopping AppHost removes its disposable database state.

HTTPS trust has separate consumers. Trust the ASP.NET development certificate in WSL's .NET/OpenSSL trust, preserving existing system roots; browser tests also require the exact Linux Chromium/NSS, Firefox and WebKit setup in `.github/workflows/verify.yml`. If opening the Aspire dashboard from a **Windows** browser, import only the exported public development certificate into that user's appropriate Windows trust store according to local policy. Linux trust does not automatically establish Windows trust. Never export an issuer private PFX for browser trust or disable certificate checks. Use the authenticated `https://localhost:17100` dashboard link printed by AppHost; normal WSL localhost forwarding must work. This is operating guidance, not a claim of an executed Windows/WSL2 manual assessment.

For the controlled login journey, the automated issuer/browser suites below provision their own synthetic credentials, callback server and temporary issuer material. A manually started pilot requires the private configuration below **and a separately configured HTTPS relying party**. There is no shipped interactive test-client URL or Admin BFF yet.

## Enable and initialize

Follow [issuer protection](issuer-protection.md) to supply distinct durable signing/encryption/wrapping credentials and a private Data Protection ring. Runtime never generates replacement issuer keys. Select `Database:Provider` as `PostgreSql` or `SqlServer` and use disposable local databases through Aspire.

Supply these settings through your private local configuration or process environment. Do not put secrets in command arguments, source, screenshots or shared logs.

| Setting | Requirement |
| --- | --- |
| `Pilot:Enabled` | Exactly `true` (case-insensitive); omitted/false keeps the health-only host. Other values fail. Enabled AuthServer requires Development. |
| `Pilot:UserName` | Explicit synthetic account name,1–128 characters; never an administrator. |
| `Pilot:Password` | External high-entropy synthetic password,16–256 characters. |
| `Pilot:ClientSecret` | External high-entropy synthetic client secret,32–256 characters. |
| `Pilot:RedirectUri` | One canonical absolute HTTPS callback, at most512 characters, no user info/query/fragment. |
| `Identity:Protection:*` | Complete existing certificate/key-ring configuration; see the issuer protection runbook. |

Environment variable names replace `:` with `__`. Aspire assigns `Identity:Issuer` from its resolved AuthServer HTTPS endpoint and supplies the least-privileged runtime database connection. It creates the one-shot `pilot-bootstrap` resource, waits for the Migrator, runs AuthServer with `--initialize-pilot`, and starts the issuer only after bootstrap succeeds. Passwords and client secrets are secret Aspire parameters; only bootstrap receives the account/client provisioning values. Certificate passwords are secret parameters for their owning hosts.

```sh
dotnet run --project src/aspire/IdentityPlatform.AppHost
```

With the private settings above present, the pilot initializes one fixed confidential web client `identityplatform-pilot`, one `pilot_api` scope and one nonprivileged account through Identity/OpenIddict managers. The versioned manifest binds their exact native IDs and security profile. Repeating initialization verifies credentials/state and records an audited no-op. It never resets a password, adopts partial records, repairs drift, grants roles or re-enables disabled objects. Failure is terminal with a sanitized message; investigate and deliberately recreate a disposable environment instead of adding a retry/repair flag.

The fixed client uses code/query, S256, `openid pilot_api`, `client_secret_basic` and resource `identityplatform-pilot-api`. Account and token routes are not management APIs. Start login from a proper HTTPS client authorization request; `/account/login` is not an unbound login entry point. The genuine test relying party is owned by the test fixture, not a shipped application or administrative BFF.

## Execute the acceptance matrix

Use the pinned SDK, tools, packages and browser build. Docker must be available and both configured provider images accessible.

```sh
dotnet tool restore
dotnet restore --locked-mode
dotnet build --no-restore --nologo
dotnet run --project tests/tooling/IdentityPlatform.BrowserInstaller --no-build -- install --with-deps chromium
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql
python3 scripts/verify.py SqlServer
python3 scripts/verify.py PostgreSql --browser chromium
python3 scripts/verify.py SqlServer --browser chromium
```

Trust the local HTTPS development certificate in both .NET and the browser trust store before provider runs. The CI workflow provisions OpenSSL and NSS trust explicitly. Never set `IgnoreHTTPSErrors`, accept every certificate, substitute an unpinned browser, or silently skip a blocked provider. The fixture keeps its test client on `127.0.0.1` and issuer on `localhost`; the development certificate must cover both, and host-only cookies remain separate.

Tests generate private temporary issuer material, synthetic passwords and disposable databases. Tokens, state, nonce and verifier stay in the test client server. Browser responses contain only protocol code/state at the registered callback and a sanitized completion message. Screenshots capture empty login pages; do not capture filled credentials. Teardown attempts every resource, checks captured container/process removal, and removes private fixture files. Required runs fail on missing browser/provider infrastructure.

The [validation quickstart](../../specs/004-first-party-login/quickstart.md) adds Firefox/WebKit, the focused regression and mutation commands. The normal workflow also runs static analysis, assurance integrity, advisory/license/secret checks and combined per-module90% line/80% branch coverage. Partial HTTP runs are useful diagnostics but do not replace browser or provider matrix acceptance. Manual assistive-technology/password-manager checks and external certification remain separate release obligations.

## Failure and lifecycle boundaries

Application-owned bootstrap, lockout/password results, session replacement and interaction completion/consumption include audit intent in their database transaction. Native OpenIddict redemption/revocation is library-owned; application audit does not claim to make every native token mutation atomic. Lost exchange responses require a new authorization flow, not a retry. Persisted token revocation does not retroactively invalidate an offline signature-only JWT validator.

The application deadline is cooperative10s, database connect/command limits5s, request body16KiB and global pilot concurrency16 with no queue. The fixed request window permits600 pilot requests/minute. Operational health/assets are exempt as documented. These are configured bounds, not an unmeasured SLA. Keep detailed provider/framework protocol logs suppressed; safe operational evidence must not contain request URLs, passwords, cookies, codes or tokens.

An interaction expires after5min, a nonsliding browser session after15min, authorization codes after60s and public tokens after5min. Fresh-login/zero-age requests need a completion for their own interaction; positive maximum age is checked again at resume. Current account/session/client/scope/authorization eligibility is checked at application decision boundaries. Session replacement invalidates delayed completed tabs. Native lifecycle claims are narrowly documented in the [claims contract](../../specs/004-first-party-login/contracts/issuer.md).

Interaction admission uses a separate five-minute `__Host-pilot-budget` cookie and a database-serialized maximum of eight live interactions. Full budgets return the existing `temporarily_unavailable` protocol error. Complete and consume a flow, or wait for expiry, before explicitly starting another; completing the password form without resuming authorization still occupies a slot. Lost admission responses can leave a committed slot occupied until expiry. Clearing a subset of flow cookies does not free server capacity. A missing budget alongside proven live flow cookies, or unknown/malformed/expired/mixed budget continuity, fails safely; malformed/unknown/expired budget cookies are removed for a new attempt. Clearing every cookie establishes new anonymous continuity, so this is not a physical-device quota or a replacement for global request limits.

For the T030 schema upgrade, stop the Development pilot, run the Migrator for the selected provider, then replace/restart all hosts together. Do not run old admission code alongside the upgraded host: readiness detects schema mismatch but is not a write barrier for old processes. Existing ownerless interactions can finish using their original binding, while a new start carrying such a live interaction is denied until it finishes or expires. Each database uses only its selected provider's checked-in migration set; both provider upgrade paths are required in verification. Runtime hosts never migrate themselves. Budget/interaction expiry is not deletion; future retention must coordinate tombstone and dependent-budget cleanup.

## Safe pilot incident diagnostics

An unexpected request failure that can still return a response produces503 with a random `X-Identity-Incident` identifier. Find Warning event1001 under `IdentityPlatform.AuthServer.Features.Protocol.Protection.PilotRequestProtection` using that same identifier. Its application payload is only `Pilot request failed. Incident {IncidentId}.`; it contains no exception, URL, OAuth state, account or credential. Do not enable suppressed framework categories to investigate a failure. T026 verified the configured pilot formatter/filter/reload and process-log paths; additional deployment sinks/enrichment require their own secrecy assessment.

Successful replacement records `session-replacement/revoked` alongside the password-verification intent in the same transaction. Its `local-session-digest` target is SHA-256 of the revoked opaque session identifier, not the raw cookie/session value. Treat this stable pseudonymous identifier as restricted audit data; it is not anonymization. No replacement event is emitted for initial login or a session row that was already revoked. Audit failure prevents the replacement and preserves the previous session.
