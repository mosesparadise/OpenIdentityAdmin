# Administrator protocol initialization

This operation is part of the unfinished internal B2 implementation (feature 011 T014). It creates registration data; it does not enable server endpoints, a browser journey or production deployment. Both-provider execution evidence remains required before acceptance.

Use the one-shot Migrator with `--Operation initialize-administrator-protocol`. Supply the already registered relational provider through `Database:Provider` and existing Platform/Sessions connection configuration through the deployment's secret mechanism. Upgrade the approved additive schema first. Initialization checks current Platform readiness, uses native OpenIddict managers and requires Platform rights for native application/scope, profile, governance coordination and security audit rows. It neither provisions roles nor runs migrations; `LocalDevelopment:ProvisionRuntimeRoles=true` is rejected. No BFF identity receives Platform rights.

Redirect a UTF-8 JSON document to standard input. The input is at most 12 KiB and must contain exactly these four fields:

```json
{
  "operationId": "e5cf245e-33c7-4d72-84cc-47650d2ad58e",
  "operatorReference": "deployment-operator-01",
  "redirectUri": "https://admin.example.test/authentication/callback",
  "publicKey": {
    "kty": "RSA",
    "kid": "administrator-assertion-01",
    "alg": "RS256",
    "use": "sig",
    "n": "REPLACE_WITH_PUBLIC_MODULUS",
    "e": "AQAB"
  }
}
```

This is an illustrative document, not executable key material. Use a fresh nonempty operation UUID, an ASCII operator reference of at most 64 letters/digits/dot/underscore/hyphen, and the exact canonical HTTPS callback. Supply a real RSA public key of at least 3072 bits with canonical unpadded base64url modulus/exponent. No private RSA members, certificates, alternate key references or additional JWK fields are accepted. Keep the private assertion key in the separate client credential store; never put it in this input, command line, repository or audit record. Issuer signing/encryption and client state protection remain separate credentials.

For an already built Migrator, an operator-controlled invocation is:

```sh
dotnet IdentityPlatform.Migrator.dll --Operation initialize-administrator-protocol < registration.public.json
```

Exit 0 reports either newly initialized or already initialized. An exact repeat verifies the entire stored registration and rolls back lock-only changes without creating another audit. Exit 2 reports invalid input; exit 1 reports an operational failure. Outputs are fixed and do not include submitted data or provider exceptions. Input/readiness has a 30-second command bound; owned work retains the existing ten-second request and five-second write bounds, including actual cleanup.

Creation atomically persists the native confidential application, scope/resource, enabled revision-1 manifest and sanitized security audit. A partial, disabled, conflicting or drifted registration is rejected and is never adopted, overwritten or repaired. The audit contains the operator reference, operation ID and fixed outcome, not key material or assertions. Lost commit acknowledgement does not trigger an automatic retry: independently inspect the committed state, then use the same expected public configuration for an intentional verification invocation.

This operation provides no key rotation, registration repair, downgrade or production activation command. Retain databases and migration histories during upgrades. Restore invalidation, bounded native token/state cleanup, complete endpoint/client interoperability and wider audit retention remain required work in the feature's [task register](../../specs/011-administrator-issuer-client/tasks.md). Follow the existing restricted backup/restore process; initialization does not make restored sessions or protocol state current.

The separate [browser registration upgrade](administrator-registration-upgrade.md) owns the audited B2-to-B3 manifest transition. Initialization remains B2-only and does not adopt or repair an upgraded registration.
