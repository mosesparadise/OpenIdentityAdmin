# Foundation quickstart

Use the SDK in `global.json`, Python 3.11+ and Docker-compatible containers. Both image registries must be reachable; SQL Server needs an x64 host with adequate memory. The AppHost uses disposable databases and separate per-run runtime passwords; there are no seeded administrator accounts or OAuth endpoints yet.

```sh
dotnet tool restore
dotnet restore --locked-mode
dotnet build --no-restore
dotnet dev-certs https --trust
dotnet run --project src/IdentityPlatform.AppHost -- --Database:Provider=PostgreSql
# Stop before switching the local dashboard profile:
dotnet run --project src/IdentityPlatform.AppHost -- --Database:Provider=SqlServer
```

On Linux, trust the dev certificate for OpenSSL/.NET as well as any browser. The SDK prints platform-specific instructions. A certificate-only export to a private scratch directory can be hashed with `openssl rehash`, then that directory added to `SSL_CERT_DIR` **alongside `/usr/lib/ssl/certs` and existing trusted directories**. Do not disable certificate validation. The CI workflow demonstrates certificate-only export; no private key is uploaded.

The authenticated Aspire dashboard uses `https://localhost:17100`; use its generated local login link. Inspect successful migrator completion and each dynamically allocated HTTPS service endpoint. `/alive` is process liveness; `/health` requires compatible storage and returns only `Healthy` or `Unhealthy`. Stop the AppHost to dispose its environment. No persistent volume is configured.

```sh
python3 scripts/check-ai-instructions.py
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql
python3 scripts/verify.py SqlServer
```

These scripts require a current build and fail on unavailable dependencies. The distributed tests allocate independent endpoints/resources and use health deadlines. The local cloud environment's SQL download-host denial is recorded in [verification.md](verification.md); commands are not proof of execution.

To collect and merge coverage, mirror `.github/workflows/verify.yml`. The `dotnet-coverage` version is in `dotnet-tools.json`. Include all three profiles before calling the aggregate result complete. A missing child-host module or an under-covered module fails the gate; generated-code exclusions are in `eng/coverage-policy.json`.

The NuGet-distributed Aspire DCP/dashboard package path is pinned and currently supported. CLI-bundle-only Aspire features are not used. Production provisioning, signing/encryption keys, backup/recovery, product users, browser authentication and release certification belong to their later approved features; this development AppHost cannot provision production principals.
