# Issuer protection operations

This runbook covers the Linux local-filesystem adapter and controlled Development pilot, not a deployable identity product. The default host profile remains health-only. The separately enabled [Development pilot](first-party-pilot.md) implements authentication, cookies, tokens and discovery/JWKS. [Feature 006 lifecycle verification](../../specs/006-issuer-lifecycle/verification.md) adds real restart and two-instance evidence, with exact acceptance/delivery states recorded there. Staged signing/encryption overlap is implemented in PR #19; its actual acceptance state remains in the linked evidence. See [the configuration contract](../../specs/002-issuer-foundation/contracts/configuration.md) and [remaining issuer work](../engineering/issuer-flow-backlog.md).

## Provision and start

Provision three different RSA key pairs: signing with explicit DigitalSignature usage, token encryption with KeyEncipherment, and Data Protection wrapping with KeyEncipherment. Each must have a private key of at least 3072 bits, a currently valid certificate, and its own password-protected PFX. These are token/storage credentials, separate from HTTPS certificates; public CA trust is not required for self-issued signing/storage certificates.

The supported PFX contains one certificate and one shrouded (encrypted) private-key bag, with a password integrity MAC. Plaintext private-key bags, MAC-only protection, ambiguous bundles/chains, absent passwords and oversized input are rejected. File size is limited to 1 MiB; the .NET loader also enforces PKCS#12 processing limits. Keep each file mode 0600 or 0400 inside a private 0700 directory. Supply passwords using a trusted configuration/secret source; do not commit them or put them on command lines. The application imports into ephemeral OS key storage **from these durable files**; it does not generate or locate replacement issuer certificates.

For a manual development certificate, OpenSSL can prompt for secrets without command-line password arguments. Run inside an already private directory outside the repository/publish/web roots, with `umask 077`:

```sh
openssl req -x509 -newkey rsa:3072 -sha256 -days 90 \
  -subj '/CN=Local issuer signing' -addext 'keyUsage=critical,digitalSignature' \
  -keyout signing.key -out signing.crt
openssl pkcs12 -export -inkey signing.key -in signing.crt -out signing.pfx
```

Generate encryption and wrapping credentials independently, with `keyUsage=critical,keyEncipherment` and different filenames/passwords. Retire unnecessary intermediate encrypted PEM material according to local secret-handling policy. Do not reuse any public key across purposes, even in a reissued certificate.

Precreate a dedicated local key-ring directory with mode 0700 under the AuthServer service identity. Configure all keys in the contract, plus the existing database configuration, then start AuthServer. Any partial issuer configuration causes fixed-message exit 2 before listening. An entirely absent issuer profile retains the existing health-only foundation mode. With both `Pilot:Enabled=false` and `Identity:AdministratorAccounts:Enabled=false`, successful protection-only startup exposes only `/alive` and `/health`. Administrator activation independently maps the implemented account pages; it does not enable pilot OIDC or Admin API/BFF access. The Development pilot separately maps its documented login/protocol routes. See the [four activation combinations and administrator runbook](administrator-accounts.md#feature-activation).

The adapter rejects symlinks/reparse points, including parent components; projected-secret symlink mounts are therefore unsupported. It rejects group/other-writable nonsticky ancestors. **Mode checks do not establish ownership**: the operator must ensure the service owns its private directories/files and trusts every ancestor owner, including a sticky shared directory's owner. The runtime cannot protect against another process with the same identity/root privileges or arbitrary concurrent filesystem replacement. Use a local private volume, not an unbounded remote filesystem; provision Windows ACL/remote-store/HSM adapters separately.

## What startup verifies

Startup loads and exercises all three private keys, checks purpose separation by public-key fingerprint, validates the persisted native key envelope before type activation, materializes every historical key (including revoked keys), probes current certificate wrapping/decryption and performs real DP protect/unprotect. It also tests private-directory write, flush, rename, read and delete. Native encrypted XML uses the pinned framework's AES-256-CBC/HMAC-SHA256 authenticated-encryptor format and certificate XML wrapping; unsupported descriptor/decryptor formats are rejected explicitly. At most 256 XML documents of at most 1 MiB each are accepted.

Data Protection may create/rotate its normal symmetric master keys. Every such key is persisted and encrypted with the configured wrapping certificate. Existing plaintext, malformed, duplicate or undecryptable keys are errors; startup never silently replaces a damaged ring. The immediate self-test proves current access; independent-provider tests establish fresh reload/decryption. Startup checks do not guarantee future disk availability or certificate validity during an indefinitely running process; the pilot token-generation guard separately checks active certificate validity and coverage of the native token expiration on every issuance. Operational health is not a proof of future issuance.

Data Protection diagnostics are suppressed even when configuration enables more-specific/provider-specific logging, including reload. This avoids framework logs that can serialize raw damaged key XML. A fixed startup diagnostic directs operators here; never troubleshoot by printing PFX/passwords, configuration objects or raw key-ring XML. Use file-presence/permission checks and controlled offline tooling under the authorized service identity.

## Wrapping-certificate rollover

1. Back up the complete ring, including revocations, and the existing decryptor through an access-controlled secret backup process. Protect credential passwords separately.
2. Provision a new wrapping PFX with a distinct key. Keep the signing/encryption credentials unchanged for this DP operation.
3. Put the old wrapper in `Identity:Protection:DataProtection:Previous:0` (up to four historical wrappers) and configure the new PFX as `Current`. Restart all instances with the same inventory and canonical issuer/application discriminator.
4. New DP master keys use the new wrapper. Existing XML files are **not re-encrypted** automatically. Normal DP key rotation remains framework-owned; this slice adds no public key-management endpoint or automatic certificate rotation service.
5. Verify restored protected state using a fresh instance. The regression suite explicitly creates a second DP key and asserts both its payload key ID and wrapper certificate; it then reloads and decrypts both generations.
6. Retain every historical private wrapping key needed by any retained key file (including revoked files) or supported backup. Expired historical wrapping certificates can decrypt; they cannot become active. Do not remove a wrapper merely because its certificate or a DP key expired. Deleting a ring can invalidate all protected state.

Updates require restart; no hot-reload/automatic activation guarantee is made. Actual cookie/code survival is covered by the separate lifecycle verification below. Token signing/encryption overlap uses the separate procedure below. Native OpenIddict certificate selection sorts by validity/expiry, so registration order must not be used as an activation policy.

## Controlled restart verification

Use the [lifecycle quickstart](../../specs/006-issuer-lifecycle/quickstart.md) on Linux or WSL2 with trusted development HTTPS. It creates disposable synthetic resources; it must not be pointed at production stores or credentials. Each provider has 13 restart cases in the dedicated xUnit v3/MTP Aspire project. The exact case-to-requirement mapping, measured results and current acceptance limits are in [verification.md](../../specs/006-issuer-lifecycle/verification.md).

The harness retains the canonical HTTPS issuer, exact relying-party callback, original database containers/connections, completed bootstrap/migrator generations, credential files and populated encrypted key ring. Only the issuer process is replaced. It requires the original launcher and actual AuthServer child to stop, distinct new process generations, a fresh healthy report and successful HTTPS response before presenting retained artifacts.

Eligible original cookies, pending antiforgery forms, completed continuations and native authorization codes must work within their original validity. No replacement login, ticket refresh, reissued code or bootstrap rerun may substitute for continuity. Invalid binding, replay, expiry and committed session/account restrictions must remain denied. Original public tokens are validated using fresh public keys; this does not imply immediate offline-token revocation.

A valid unrelated wrapping certificate without the historical decryptor must fail startup with the fixed safe protection diagnostic and exit code 2, without readiness. The test restores the original wrapping material, verifies unchanged ring/credential content and proves original-cookie recovery. Operational recovery must likewise retain the correct ring and decryptors; generating fresh keys cannot repair old protected state.

The test helper has a 45-second restart budget; live-code controls require more than 10 seconds of remaining original validity. These are verification bounds, not production availability or recovery objectives. CI runs standard, Restart and Distribution jobs separately for both providers and requires all eight lifecycle coverage artifacts. Interrupted/cancelled runs do not establish continuity or clean teardown. Controlled two-node loss is covered below; production failover guarantees, backup restore/RPO/RTO, human accessibility and certification remain separate acceptance obligations.

## Recovery and evidence

Restore the entire ring and its retained wrappers under the same canonical issuer and protection purposes; preserve revocations. A different issuer/application discriminator or missing master key cannot decrypt existing payloads. Do not generate a fresh ring to conceal a failed restore. Restores and production changes need their normal operational authorization.

Run the [feature quickstart](../../specs/002-issuer-foundation/quickstart.md). It includes native MTP unit/actual-host checks and both credential-enabled Aspire database profiles. Test fixtures are synthetic and temporary; never substitute production credentials. Coverage and test results do not establish external security assessment or OIDF certification.


## Controlled two-instance verification

The lifecycle project’s Distribution story uses two real AuthServer processes on one disposable Aspire graph, with one canonical HTTPS issuer/callback, least-privileged platform connection and retained private material. All 23 distribution cases passed on each provider at the reviewed source checkpoint, alongside the complete 1,097-case ordinary matrix. Its exact source evidence and final PR delivery gate are recorded in [lifecycle evidence](../../specs/006-issuer-lifecycle/verification.md).

Run `python3 scripts/verify.py PostgreSql --partition lifecycle --story Distribution` (or `SqlServer`) after the [quickstart](../../specs/006-issuer-lifecycle/quickstart.md). The test-only transport routes to a listener proved to belong to the selected process generation while preserving canonical Host, normal TLS, cookies and protocol validation. It validates every request and redirect; a client bound to an old process is rejected after replacement. Tests create a fresh pinned client for the new process, without changing its issuer identity.

The suite checks genuine cross-node browser/code state, concurrent native single-use decisions, committed eligibility at authorization and token exchange, and either-node loss. Bad/missing credential controls prove safe startup failure and original-cookie recovery. A valid incompatible encryption certificate starts successfully but must reject the other node’s original code; restoration must recover that exact code before its unchanged expiry. This demonstrates why operational readiness alone cannot establish shared cryptographic identity.

Never infer a production proxy or shared rate-limit design from this harness. Rate/concurrency limits remain per process. There is no automatic retry of an ambiguous security mutation, production load-balancer configuration, throughput/SLA claim or backup-restore evidence. Staged signing/encryption acceptance is tracked separately in the same evidence record.


## Signing and token-encryption rollover

This is a controlled operator procedure for the current five-minute pilot profile. It is not automatic cluster key management. The root `Signing:Path/Password` and `Encryption:Path/Password` remain the explicit active keys. Each purpose optionally accepts one `Staged:Path/Password` pair and up to four contiguous `Previous:0..3:Path/Password` pairs. Every credential must have a different RSA public key, including across purposes and DP wrappers. The original single-active configuration remains valid.

1. Provision distinct new signing/encryption PFXs under the same private-file controls. Configure old active plus new Staged on every node and replace each process. Staged may be future-dated but cannot be expired; old Previous may be expired but cannot be future-dated. The native library publishes public signing material and loads all validation/decryption keys. Explicit active selection prevents a later-expiring staged certificate from silently becoming active.
2. Confirm exact overlap JWKS on every node and obtain acknowledgement from every participating relying party that it holds the new public key. The test's frozen overlap snapshot proves this bounded condition; fetching JWKS once does not establish universal cache convergence. Retain a stale old-only verifier as a negative control.
3. Activate on one node by moving the new pair to the root active slots, moving old to Previous, and removing the duplicated Staged slots. Stop, update private configuration atomically, and start. Verify old-to-new and new-to-old artifacts, exact signing/encryption key IDs and unchanged original expiry. Then activate the remaining nodes. Configuration reload alone does not activate credentials.
4. Record the conservative last-old-issuance boundary only after **every** known old-active process has terminated. Retain old signing/decryption material strictly beyond that boundary plus 300 seconds and beyond every original affected expiration. The controlled test requires more than 300 seconds of monotonic elapsed time too; a wall-clock adjustment cannot substitute for the wait. Real deployments must add their declared clock skew, RP cache/propagation, outstanding artifact, rollback and backup allowances. The current test uses zero server/public-verifier skew and acknowledged public caches; do not extrapolate that to an unbounded client population.
5. Prove original artifacts expire through native validation before removing old keys. Remove from both nodes, replace them, and verify new-only public keys and new issuance, while original eligible DP cookies still work. Preserve separately required backup keys and the complete DP ring/wrappers.

For rollback, select the old pair only while it remains valid and can cover each issued token's original lifetime. Retain the newer pair as Staged/Previous so newer artifacts still work. Rollback resets the last-old-issuance boundary and waiting period. Old-only rollback can reject already-issued new codes and cached requests; never compensate with reissued artifacts, a fresh ring, longer lifetimes or ambiguous mutation retries.

The host cannot infer that an operator omitted an entire historical slot: there is no durable cluster inventory ledger. Premature signing publication removal breaks fresh verifiers while cached verifiers can still accept live tokens. Premature encryption removal causes native decryption denial; restoring the correct historical key can recover the exact original artifact only before its original expiry. Readiness does not prove overlap correctness.

Issuance fails safely if the active key is missing, unsuitable, expired or cannot cover the native original expiration, or if native principal lifetimes exceed this pilot's bounds (60-second code; 300-second request/access/ID). It neither selects a historical fallback nor changes token dates. The guard runs before the affected token entry is created, but native code redemption and earlier token entries can already have occurred when a later generation fails. No response credential is returned; this is not an atomic transaction rollback. Diagnose with fixed errors and controlled native state inspection, never credential dumps. Recover with corrected material and a fresh legitimate flow; do not replay an ambiguously consumed code.
