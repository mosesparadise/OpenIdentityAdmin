# Validate durable issuer protection

Use the pinned .NET SDK, Docker and trusted local development HTTPS certificate from the foundation quickstart. Restore/build with the repository lock files:

```bash
dotnet restore --locked-mode
dotnet build --no-restore
python3 scripts/verify.py unit
python3 scripts/verify.py PostgreSql
python3 scripts/verify.py SqlServer
```

Host tests create synthetic external PFX files and private temporary directories, execute actual RSA/Data Protection operations, dispose/reload providers and launch the real AuthServer process. Aspire tests inject those files into the existing isolated provider environments. Secrets and protected payloads are not test output; failures retain only sanitized evidence. Full CI collects child-process coverage and checks per-module floors.

For an operator-managed host, provision three separate RSA credentials and a private local key directory outside the checkout. Configure the keys in [the contract](contracts/configuration.md) through trusted configuration, alongside the existing database connection. Run `dotnet run --project src/backend/IdentityPlatform.AuthServer` with that configuration. Valid credentials permit the existing health endpoints; no authentication endpoint is enabled. Bad configuration exits 2 before listening. Do not pass passwords as CLI arguments.

See [the protection runbook](../../docs/operations/issuer-protection.md) for storage and rollover. Empty health-only profiles remain useful for the existing foundation; they are not an issuer deployment. Full login/code-flow work remains in [the issuer backlog](../../docs/engineering/issuer-flow-backlog.md).
