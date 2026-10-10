# Issuer protection configuration contract

All paths are absolute. Supply values via trusted configuration/secret injection, never command-line secrets or committed JSON. An entirely absent Identity issuer/protection section retains the foundation health-only profile. Any presence requires the complete profile below.

| Configuration key | Meaning |
| --- | --- |
| `Identity:Issuer` | Canonical absolute HTTPS issuer; existing Core validation applies. |
| `Identity:Protection:Signing:Path` / `Password` | Active signing PFX (RSA >=3072, DigitalSignature). |
| `Identity:Protection:Encryption:Path` / `Password` | Active token encryption PFX (RSA >=3072, KeyEncipherment). |
| `Identity:Protection:DataProtection:Current:Path` / `Password` | Active DP wrapping PFX (RSA >=3072, KeyEncipherment). |
| `Identity:Protection:DataProtection:Previous:{0..3}:Path` / `Password` | Optional historical wrapping PFXs; expired certificates can decrypt. |
| `Identity:Protection:DataProtection:KeyRingPath` | Existing private local Linux directory, mode 0700. |

Environment variable names replace colons with double underscores. Password values must be nonempty and come from the operator's secret source. The application validates existing permissions; it does not fix them or establish file ownership. PFX input is limited to 1 MiB, one certificate and one encrypted private-key bag with a password MAC; certificate chains are not accepted by this adapter. The ring accepts at most 256 XML documents using the pinned framework encrypted descriptor format. Credentials must use different public keys for each purpose, including prior wrappers. Certificates/paths/DP contents are never included in errors. Configuration updates require restart.

Runtime surfaces remain `/alive` and `/health` only. There is no discovery/JWKS, authorization/token or account route in this increment. Credential validation failure exits 2 before listening with a fixed diagnostic; no automatic replacement keys or retry. Data Protection generates only its normal encrypted durable master keys after successful configuration.

The first adapter supports Linux. Symlinks are rejected. The operator must ensure trusted ancestors, same service identity for authorized nodes, private backups, and local storage availability. This is not a complete production provisioning/rotation service.
