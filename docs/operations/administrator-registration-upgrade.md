# Administrator browser registration upgrade

This explicit one-shot operation is for the internal B3 transition. It is not authorization to upgrade a production database. Current live issuer composition still selects B2 and rejects the upgraded manifest; wait for accepted B3 composition and an authorized deployment plan before applying it to an active installation. The [implementation and evidence record](../../specs/013-administrator-browser-session/registration-upgrade.md) distinguishes pending provider acceptance from source verification.

Use an already built Migrator with `--Operation upgrade-administrator-browser-registration`. Supply the registered relational provider and Platform connection through the existing deployment configuration/secret mechanism. The command uses only Platform, requires its current schema, and does not provision roles or run migrations. `LocalDevelopment:ProvisionRuntimeRoles=true` is rejected. Keep databases and migration histories intact.

The operator role needs the existing registration mutation/coordination rights, native token metadata read/revocation rights and **SELECT plus INSERT on the security-audit table**. SELECT is required to recognize a prior exact operation; INSERT alone is insufficient. Scope role changes through the normal deployment process; the command does not grant itself privileges. No BFF role receives Platform access.

Redirect at most 12 KiB of UTF-8 JSON with exactly these five fields:

```json
{
  "operationId": "4a7a6034-1be7-47f9-8f61-29a62a70a798",
  "operatorReference": "deployment-operator-01",
  "expectedRevision": 1,
  "redirectUri": "https://admin.example.test/authentication/callback",
  "publicKey": {
    "kty": "RSA",
    "kid": "administrator-assertion-01",
    "alg": "RS256",
    "use": "sig",
    "n": "REPLACE_WITH_EXISTING_PUBLIC_MODULUS",
    "e": "AQAB"
  }
}
```

This illustrative document contains no executable key. Use the **existing exact public** callback/key and a current canonical positive revision less than 9223372036854775807, with a new nonempty operation UUID and the bounded ASCII operator reference. Keep this exact input for intentional reconciliation. Duplicate/unknown fields, decimal/exponent revision forms, private-key members, oversized/invalid encoding and callback collision with the fixed logout return are rejected. The same RSA/canonical URI constraints as [initialization](administrator-protocol-initialization.md) apply. Never provide credentials/private keys in stdin, arguments, source or audit.

```sh
dotnet IdentityPlatform.Migrator.dll --Operation upgrade-administrator-browser-registration < upgrade.public.json
```

Exit 0 emits either `Administrator browser registration upgraded.` or `Administrator browser registration upgrade already applied.` The latter requires exact current B3 metadata at original revision +1 and exactly one matching sanitized receipt for the same operator/operation. It makes no persistent change. Exit 2 reports invalid input; exit 1 reports operational failure. Outputs are fixed and omit submitted values and provider exceptions. Input/readiness is bounded at 30 seconds; owned work retains the existing request/write/cleanup deadlines.

An exact B2 registration advances once to the internally derived B3 manifest, with native EndSession permission and `/bff/logout/return` at the original HTTPS callback authority. Profile enabled state is preserved, including an already disabled profile at a later revision. Disabled native application/scope markers, partial or extra metadata and mismatched revision/key/callback are unsupported drift and fail unchanged. The command never repairs metadata or enables an account/profile.

Upgrade invalidates old pending PAR rows and advances the profile revision used by old authorization codes; it does not bulk-revoke access/ID/code rows or unrelated applications. One transaction contains native metadata, request revocation, revision and audit. On failure, do not assume rollback or rerun automatically: lost commit acknowledgement can leave a durable upgrade. Independently inspect restricted state/receipt evidence, then deliberately invoke the **same original input** to reconcile. A different operation/operator/revision is not a substitute. If exact state or receipt does not match, stop and investigate; there is no force, repair or downgrade option.

All historical/human, activation and production/public-release gates remain open. This command alone does not deliver product login/logout, refresh, management API authority or React integration.
