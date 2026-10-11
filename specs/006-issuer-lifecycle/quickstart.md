# Lifecycle verification quickstart — pending implementation

No lifecycle test project or command exists yet. Use the established [pilot quickstart](../004-first-party-login/quickstart.md) for the existing suite and [issuer protection runbook](../../docs/operations/issuer-protection.md) for its durable-key prerequisites. These commands do not prove M01-E.

The planned lifecycle harness uses the pinned SDK, Linux/WSL2 private key adapter, real PostgreSQL and SQL Server, trusted development HTTPS and ephemeral Aspire environments. It replaces only the issuer child while retaining databases and private material, then verifies all process/container cleanup. Do not use production identities or secrets, disable TLS, increase artifact lifetimes or restart the entire disposable AppHost to simulate issuer continuity.

The exact project/runner/CI commands and report paths will be documented after the user's structural choice and implementation. First acceptance is planned for restart continuity; two-node and overlap cases remain separate pending work unless the user selects a combined PR. The [plan](plan.md) records the proposed cases and gates. No manual Windows/assistive-technology, backup-restore, two-node or key-rotation evidence is claimed by this draft.
