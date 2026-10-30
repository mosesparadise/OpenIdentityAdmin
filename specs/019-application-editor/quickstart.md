# Validate application creation and editing

This feature is under implementation on `123-application-editor`; the accepted main currently provides the read-only list. Do not treat these planned validation steps as completed acceptance.

Use the existing native Aspire `local-demo` profile, initialize an administrator and enroll MFA as described in [the local guide](../014-local-b3-demo/quickstart.md). No custom PowerShell or separate frontend build is planned.

After implementation, open Applications and Create application. Supply a unique client ID, display name, exact HTTPS callback and explicit UTC secret expiry. Capture the newly shown secret once in the integrating application's local secret store; no later GET can recover it. Open details, edit display/callback fields, verify a second stale editor is rejected, and use explicit secret replacement if the first output was lost. Default PostgreSQL and `-- --Database:Provider=SqlServer` must both pass.

The real integrating client must use discovery, confidential code flow, PKCE S256, nonce/state and only openid. Every new sign-in performs fresh administrator password/MFA and explicit consent. The resulting client identity contains no management scope/audience/roles. A successful saved row alone is not this proof; final verification will link the real hosted/native journey.

Actual Windows, manual accessibility and all release/historical gates remain separate. Refresh, ordinary API scopes, other profiles and ordinary-user onboarding remain outside this increment.
