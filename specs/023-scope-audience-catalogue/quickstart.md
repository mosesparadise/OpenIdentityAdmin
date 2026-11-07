# Scope/audience catalogue validation

**Planned journey; not yet implemented or accepted.** Existing application management
remains available. The selected batch adds Scopes/Audiences and scope assignment without
custom scripts or a separate launch model.

```sh
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

Use disposable synthetic data, normal dashboard initialization and genuine password/MFA.
After implementation: create one audience, an API scope and an identity scope with email
policy; assign them to a managed application; complete genuine consent and native token
exchange. Validate exact audience/scopes/signatures and query UserInfo GET/POST. Exercise
stale edits, reference-blocked deletion and policy-change denial. Run on both providers.

Do not use administrator bearer tokens in browser JavaScript. Do not manually alter the
database to bypass missing UI or migrations. UserInfo/claim changes must preserve existing
OpenID-only and BFF login behavior. Offline tokens retain documented expiry limits.

Actual commands, report hashes and UI/provider acceptance will be recorded in
verification.md. Windows/manual/Firefox/A-009 and existing external gates stay open.
