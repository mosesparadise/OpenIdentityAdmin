# Configuration and persisted-state model

There are no new application database entities or migrations.

| Entity | Fields and invariants |
| --- | --- |
| Issuer protection profile | Exact absolute HTTPS issuer, without userinfo/query/fragment. Complete profile required when any issuer/protection setting is present. |
| Active certificate reference | Absolute PFX path; nonempty externally injected password. Certificate has RSA private key >=3072 bits; now in [NotBefore, NotAfter); explicit DigitalSignature for signing or KeyEncipherment for token/DP encryption. |
| Historical wrapping reference | Same path/password/key constraints; expired validity allowed for decryption; no reuse across purposes or duplicate public keys. At most four historical wrappers. |
| Key-ring directory | Existing absolute, non-symlink Linux directory with mode 0700; issuer-specific local storage. Operator controls safe ancestors and backup access. |
| Native DP key | Framework-owned key identifier, dates and encrypted descriptor. Plaintext master-key XML is never accepted. Revocation semantics remain framework-owned. |
| Loaded material | Owned certificate objects live until host/provider disposal; partial-load exceptions release already loaded objects. No ToString/value logging. |

State transitions: absent profile → health-only; complete valid profile → loaded → ring verified → listener; any validation/probe failure → sanitized exit 2 with no listener. Wrapping rollover: old active → new active plus retained old → new DP key generation → both generations decrypt after reload. Historical certificates cannot be removed while retained keys/backups require them. Existing old DP keys are not automatically rewrapped.
