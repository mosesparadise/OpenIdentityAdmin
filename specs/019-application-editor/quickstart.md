# Try application creation and editing locally

The create/details/edit/secret-replacement UI is implemented on **`123-application-editor`**. Merged `main` still has the read-only Applications list. Browser/native Aspire acceptance for this new UI remains pending; [current evidence and limits](editor-recovery.md) distinguish delivered source from unperformed testing.

Check out the feature branch in a clean checkout, then set **IdentityPlatform.AppHost** as the IDE startup project, select **local-demo** and run. Alternatively, from the repository root:

```text
dotnet tool restore
dotnet restore --locked-mode
dotnet dev-certs https --trust
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

Use Docker Desktop with Linux containers and a current .NET10.x SDK. No custom PowerShell script or separate frontend build/server is needed: the generated UI is committed and embedded. PostgreSQL is the default. For SQL Server, append `-- --Database:Provider=SqlServer`; its container requires a supported x64 host.

Follow the [native Aspire local guide](../014-local-b3-demo/quickstart.md) to initialize a synthetic administrator from the dashboard, enroll MFA and sign in. Open <https://bff.dev.localhost:17444/applications>.

1. Choose **Create application**. Enter a unique client ID, display name, exact HTTPS callback(s), and your chosen future UTC expiry in `YYYY-MM-DDTHH:mm:ssZ` format. No expiry is inferred.
2. Submit once. Save the displayed client secret in the integrating application's local secret store before hiding or leaving the view. Switching tabs also clears the output.
3. Choose **I’ve saved it — hide secret** or return to Applications and choose the new registration's **View details**. Later details never retrieve the secret.
4. From managed application details, choose **Edit application** to change the display name or exact HTTPS callbacks. Client ID remains permanent. A stale-version response requires **Reload current details** and an explicit new edit; the previous draft is not reapplied.
5. Choose **Replace secret** to recover a lost secret or rotate it. Choose a new future UTC expiry and confirm that the current secret will stop working immediately. Save the new one-time output and update your integrating application's secret store. Replacement requires current credential permission and recent MFA; sign in again if required.
6. If any response is interrupted, inspect the list/current details before explicitly submitting another mutation. A change may already have committed. An undisplayed secret cannot be recovered, but can be explicitly replaced from current details. No mutation retries automatically.

The integrating client must use discovery, confidential code flow, Basic client authentication, PKCE S256, nonce/state and only `openid`. Every new sign-in requires fresh password/MFA and explicit consent. The resulting client has no management scopes, audience or roles. [T012](native-issuance.md) and [T013](created-client-journeys.md) record genuine created-client issuer verification on both providers.

This disposable demo loses accounts/databases/keys when the whole graph is restarted. Actual Windows, browser/manual accessibility, all six historical findings and release gates remain open. Refresh, ordinary API scopes, other profiles and ordinary-user onboarding remain outside this increment.
