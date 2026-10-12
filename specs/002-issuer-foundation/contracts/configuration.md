# Issuer protection configuration contract

All paths are absolute. Supply values via trusted configuration/secret injection, never command-line secrets or committed JSON. An entirely absent Identity issuer/protection section retains the foundation health-only profile. Any presence requires the complete profile below.

| Configuration key | Meaning |
| --- | --- |
| `Identity:Issuer` | Canonical absolute HTTPS issuer; existing Core validation applies. |
| `Identity:Protection:Signing:Path` / `Password` | Active signing PFX (RSA >=3072, DigitalSignature). |
| `Identity:Protection:Encryption:Path` / `Password` | Active token encryption PFX (RSA >=3072, KeyEncipherment). |
| `Identity:Protection:{Signing|Encryption}:Staged:Path` / `Password` | Optional next token credential; publication/decryption only until explicitly activated. |
| `Identity:Protection:{Signing|Encryption}:Previous:{0..3}:Path` / `Password` | Optional historical token credentials; contiguous indices from zero, up to four per purpose. |
| `Identity:Protection:DataProtection:Current:Path` / `Password` | Active DP wrapping PFX (RSA >=3072, KeyEncipherment). |
| `Identity:Protection:DataProtection:Previous:{0..3}:Path` / `Password` | Optional historical wrapping PFXs; expired certificates can decrypt. |
| `Identity:Protection:DataProtection:KeyRingPath` | Existing private local Linux directory, mode 0700. |

Environment variable names replace colons with double underscores. Password values must be nonempty and come from the operator's secret source. The application validates existing permissions; it does not fix them or establish file ownership. PFX input is limited to 1 MiB, one certificate and one encrypted private-key bag with a password MAC; certificate chains are not accepted by this adapter. The ring accepts at most 256 XML documents using the pinned framework encrypted descriptor format. Credentials must use different public keys for each purpose, including staged/historical token keys and prior wrappers. Active token certificates must be currently valid; staged certificates may be future-dated but not expired; historical token certificates may be expired but not future-dated. Partial, unknown, duplicate or noncontiguous token slots are rejected. Certificates/paths/DP contents are never included in errors. Configuration updates require restart.

With the pilot disabled, runtime surfaces remain `/alive` and `/health` only. The separately enabled Development pilot adds its documented protocol/account routes. Native OpenIddict publishes all configured public signing keys; explicit active selection controls issuance, with validity and original token-expiry guards before token entry creation. See [the key lifecycle contract](../../006-issuer-lifecycle/contracts/key-lifecycle.md) and [runbook](../../../docs/operations/issuer-protection.md). Credential validation failure exits 2 before listening with a fixed diagnostic; no automatic replacement keys or retry. Data Protection generates only its normal encrypted durable master keys after successful configuration.

The first adapter supports Linux. Symlinks are rejected. The operator must ensure trusted ancestors, same service identity for authorized nodes, private backups, and local storage availability. This is not a complete production provisioning/rotation service.
