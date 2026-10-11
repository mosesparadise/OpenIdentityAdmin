# Issuer protection operations

This runbook covers the first Linux local-filesystem adapter, not a deployable identity product. The default host profile remains health-only. The separately enabled [Development pilot](first-party-pilot.md) now implements authentication, cookies, tokens and discovery/JWKS. Public signing/encryption overlap and full session/code/token restart/two-instance acceptance remain M01-E; they are not established by this protection adapter. See [the configuration contract](../../specs/002-issuer-foundation/contracts/configuration.md) and [remaining issuer work](../engineering/issuer-flow-backlog.md).

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

Precreate a dedicated local key-ring directory with mode 0700 under the AuthServer service identity. Configure all keys in the contract, plus the existing database configuration, then start AuthServer. Any partial issuer configuration causes fixed-message exit 2 before listening. An entirely absent issuer profile retains the existing health-only foundation mode. With `Pilot:Enabled=false`, successful protection-only startup exposes only `/alive` and `/health`; enabling the separate Development pilot also maps its documented login/protocol routes.

The adapter rejects symlinks/reparse points, including parent components; projected-secret symlink mounts are therefore unsupported. It rejects group/other-writable nonsticky ancestors. **Mode checks do not establish ownership**: the operator must ensure the service owns its private directories/files and trusts every ancestor owner, including a sticky shared directory's owner. The runtime cannot protect against another process with the same identity/root privileges or arbitrary concurrent filesystem replacement. Use a local private volume, not an unbounded remote filesystem; provision Windows ACL/remote-store/HSM adapters separately.

## What startup verifies

Startup loads and exercises all three private keys, checks purpose separation by public-key fingerprint, validates the persisted native key envelope before type activation, materializes every historical key (including revoked keys), probes current certificate wrapping/decryption and performs real DP protect/unprotect. It also tests private-directory write, flush, rename, read and delete. Native encrypted XML uses the pinned framework's AES-256-CBC/HMAC-SHA256 authenticated-encryptor format and certificate XML wrapping; unsupported descriptor/decryptor formats are rejected explicitly. At most 256 XML documents of at most 1 MiB each are accepted.

Data Protection may create/rotate its normal symmetric master keys. Every such key is persisted and encrypted with the configured wrapping certificate. Existing plaintext, malformed, duplicate or undecryptable keys are errors; startup never silently replaces a damaged ring. The immediate self-test proves current access; independent-provider tests establish fresh reload/decryption. Startup checks do not guarantee future disk availability or certificate validity during an indefinitely running process; future issuance/operational-health features must enforce their own time policy.

Data Protection diagnostics are suppressed even when configuration enables more-specific/provider-specific logging, including reload. This avoids framework logs that can serialize raw damaged key XML. A fixed startup diagnostic directs operators here; never troubleshoot by printing PFX/passwords, configuration objects or raw key-ring XML. Use file-presence/permission checks and controlled offline tooling under the authorized service identity.

## Wrapping-certificate rollover

1. Back up the complete ring, including revocations, and the existing decryptor through an access-controlled secret backup process. Protect credential passwords separately.
2. Provision a new wrapping PFX with a distinct key. Keep the signing/encryption credentials unchanged for this DP operation.
3. Put the old wrapper in `Identity:Protection:DataProtection:Previous:0` (up to four historical wrappers) and configure the new PFX as `Current`. Restart all instances with the same inventory and canonical issuer/application discriminator.
4. New DP master keys use the new wrapper. Existing XML files are **not re-encrypted** automatically. Normal DP key rotation remains framework-owned; this slice adds no public key-management endpoint or automatic certificate rotation service.
5. Verify restored protected state using a fresh instance. The regression suite explicitly creates a second DP key and asserts both its payload key ID and wrapper certificate; it then reloads and decrypts both generations.
6. Retain every historical private wrapping key needed by any retained key file (including revoked files) or supported backup. Expired historical wrapping certificates can decrypt; they cannot become active. Do not remove a wrapper merely because its certificate or a DP key expired. Deleting a ring can invalidate all protected state.

Updates require restart; no hot-reload/automatic activation guarantee is made. Token signing/encryption key overlap, staged public JWKS publication and actual cookie/code survival belong to the next protocol feature. Native OpenIddict certificate selection sorts by validity/expiry, so registration order must not be used as an activation policy.

## Recovery and evidence

Restore the entire ring and its retained wrappers under the same canonical issuer and protection purposes; preserve revocations. A different issuer/application discriminator or missing master key cannot decrypt existing payloads. Do not generate a fresh ring to conceal a failed restore. Restores and production changes need their normal operational authorization.

Run the [feature quickstart](../../specs/002-issuer-foundation/quickstart.md). It includes native MTP unit/actual-host checks and both credential-enabled Aspire database profiles. Test fixtures are synthetic and temporary; never substitute production credentials. Coverage and test results do not establish external security assessment or OIDF certification.
