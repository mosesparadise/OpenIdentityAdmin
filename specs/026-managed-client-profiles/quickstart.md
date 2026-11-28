# Connect an installed app

Use this profile for an installed app that receives sign-in callbacks at an HTTPS URL
on a domain you control. Configure the operating system’s association between that
domain and the app. Verify on a device that opening the callback URL launches the
intended app. The identity server checks the registered redirect URI. Device routing
is the app owner’s responsibility.

## Register the callback

1. In the administrator workspace, open **Applications**, choose **Create application**
   and select **Public native application**.
2. Enter a permanent client ID, a display name the user will recognize, and one exact
   HTTPS callback per line. Save the registration.
3. Open **Client integration settings** in the application details. Copy the client ID
   and one registered redirect URI to the installed app’s OIDC library. Use the
   deployment’s configured issuer for discovery.

For example, an app registered as `native-application-link` with callback
`https://mobile.example.test/callback` uses the following settings. Substitute the
values shown for your registration.

| OIDC setting | Example |
| --- | --- |
| Client ID | `native-application-link` |
| Redirect URI | `https://mobile.example.test/callback` |
| Response type | `code` |
| PKCE challenge method | `S256` |
| Token endpoint authentication method | `none` |
| Initial scope | `openid` |

## Sign in and verify the result

Start authorization in the system browser. Let the OIDC library generate fresh
state, nonce and PKCE verifier values for this sign-in. Keep the verifier in the app
until the callback arrives. The user signs in, completes MFA and approves consent.

On return, let the library validate the callback and exchange the code with the same
redirect URI, client ID and original verifier. Omit client secret and HTTP Basic
credentials. Have the library validate the ID token against the configured issuer,
client ID and nonce. Use the access token for UserInfo and match its returned
subject to the validated ID token subject.

To call an API, first have an administrator assign an enabled scope for that API to
the registration. Request the scope during a new sign-in. For example, requesting
`openid orders.read` after assigning `orders.read` for `orders-api` yields an access
token for that audience. An assigned `email` scope must also be requested and
consented to before UserInfo can return the available email.

The [two-provider journey](../../tests/aspire/IdentityPlatform.Administrator.Tests/Features/ManagedApplications/NativeClientJourneyTests.cs)
exercises registration, consent, exchange and UserInfo with synthetic accounts.

## Public browser client

In **Applications**, choose **Create application** and **Public browser application**.
Supply the exact HTTPS redirect URI used by the OIDC library. Enter each HTTPS origin
from which the application will fetch discovery, signing keys, tokens or UserInfo.
Origins contain the scheme, host and any non-default port. A callback such as
`https://signin.example/callback` and an origin such as `https://portal.example`
are separate inputs. Registering a callback grants no cross-origin access.

Configure the OIDC library with the deployment issuer, saved client ID and redirect
URI, response type `code`, scope `openid`, PKCE `S256` and token endpoint
authentication method `none`. The library should generate fresh state, nonce and
verifier values for each sign-in and verify them on return. Open the authorization
endpoint through top-level navigation. For cross-origin fetches, omit cookies.
Send the access token to the discovered UserInfo endpoint in the Authorization
bearer header. Disabling or retiring the registration withdraws its access.


## Confidential machine client

In Applications, choose **Machine client**. Register a service client ID, a display
name, and an explicit UTC secret expiry. Copy the one-time secret to the service’s
secret store. Assign at least one enabled API scope and configure the service with
the issuer address, client ID and secret. Each token request can target one API
audience. For example, assign `orders.read` for `orders-api`.

Use OAuth client credentials with HTTP Basic client authentication. Send a form
containing `grant_type=client_credentials&scope=orders.read` to the HTTPS token
endpoint advertised in issuer discovery. Use the returned `access_token` as the
bearer for `GET /orders`. The sample orders API validates the signature, issuer,
audience, lifetime and `orders.read` scope. The token represents the service and
contains `identity_type=machine`. The client receives its expiry in `expires_in`.

Replacement immediately stops the old secret from obtaining another token.
Disablement and permanent retirement also stop new exchanges. An API performing
offline validation can accept a previously issued token until its expiration.

Each successful machine exchange retains one native access-token row. All managed
registrations share the existing 20,000-row token budget, including expired rows.
At the limit the issuer refuses new issuance. Services should reuse a token for
its authorized audience and scopes during its advertised lifetime. This increment
provides no scheduled cleanup of expired machine tokens.
