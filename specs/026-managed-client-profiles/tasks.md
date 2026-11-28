# Implementation register

- [x] T001 Provide managed web integration settings from the authorized registration with a credential placeholder. Verify rendering, callback binding and read-only/terminal exclusions.
- [ ] T002 Add browser and native registration and authorization-code journeys, including exact redirect, separate browser origin policy, S256, and lifecycle regression tests.
- [ ] T003 Deliver the client-credentials protocol and registration together, including a machine-only identity and bounded assigned API scopes.
- [ ] T004 Deliver device registration with verification, approval, polling, throttling, expiry and single redemption on both providers.
- [ ] T005 Implement asymmetric public-key registration, replacement and replay-resistant native client authentication with provider upgrade, concurrency and audit coverage.
- [ ] T006 Extend application integration settings, examples and generated versioned contracts for each delivered profile. Implement permission-aware documentation after resolving viewer activation dependencies.
- [ ] T007 Run required provider, browser, contract, mutation, coverage and supply-chain checks. Independently review the original evidence and record accepted scope in the roadmap.

### Verification for the managed web integration view

T001 was squash-merged to main in PR141. The UI build, type check, asset
reproducibility check and all 149 existing UI cases passed. The component test
checks the complete settings, multiple callbacks, the secret placeholder and
read-only/retired exclusions. A local Chromium 151.0.7922.173 run of the shipped
bundle used synthetic BFF responses and a 320-pixel viewport. Keyboard opening,
focus, long callback reflow, page-hide clearing and observation expiry passed.
The shared authenticated editor journey now checks the current saved callback,
keyboard disclosure and page-hide removal after actual credential replacement.
The hosted provider/browser matrix and original CI evidence review passed for PR141. See [verification](verification.md).

### Profile storage prerequisite for T002–T005

Both Platform provider migrations can now represent public native, browser and device
registrations without credential timestamps or a credential revision. Confidential web
and machine profiles with a shared secret or a public authentication key require a
positive credential revision and an ordered lifetime. At the time of this storage increment, application and issuer eligibility
required the exact delivered web profile.

Local Release persistence tests ran 14 cases, seven on each provider. They checked
the profile combinations, partial metadata, alias rejection, populated predecessor
upgrades, native credential preservation, nonempty retirement history, repeated upgrades
and guarded downgrade. A downgrade of a database containing a newer profile fails
atomically. The provider-independent row and schema comparison covers both migration
histories. Original TRX SHA-256 was
`f1f89dc8b59237a5d2bf54d4aa90608cb10c101db876070ce003253f11b8d48c`.

PR142 delivered the storage prerequisite at `7461cdd`. The final CI638 run
passed all 149 jobs. Both persistence reports contained 42 passing cases,
including seven affected cases per provider. [Storage verification](verification.md#client-profile-storage)

### Public native registration delivered

PR143 reached main at `3453e98` with reviewed CI656 evidence. This delivers
the native portion of T002 and the installed app guide in T006. The browser
portion of T002 and the remaining documentation in T006 are still open.
[Acceptance](verification.md#public-native-registration) · [Installed app guide](quickstart.md)

T002 and T006 now include a separate public native create operation on the versioned
API and BFF. The editor submits the client ID, display name and exact HTTPS callbacks.
The issuer binds a credential-free code exchange to the current public registration,
exact redirect and S256 verifier. The application owner configures the operating
system's association for its HTTPS callback. The server validates the registered URL
and its exact use. The integration example specifies token endpoint authentication
method `none` and omits a client secret.

Local verification covered 44 native contract cases, 47 contract tooling cases, 45
application input/editor cases, 150 UI cases and all 93 test selection cases. Both
generated client checks and the six-asset UI reproducibility check passed. Lifecycle
and uncertainty projections include public registrations with no credential metadata.

The protected BFF registration, real human consent, wrong Basic credential rejection,
secretless S256 exchange and user-bound token verification passed once on each provider.
The original native journey TRX SHA-256 is
`90e57b128387db901471fef81eea1c76819ed9169ea061fe9373cdf69323938d`.

The expanded native journey passed on both providers. After the original identity-only
exchange it read the empty assignment, checked that secret replacement was refused,
assigned email and an API scope through the BFF and read back revision two. Fresh
consent for only the API scope produced a token for its audience. UserInfo returned the
subject without the unrequested email. Original TRX SHA-256 `9cbb90c47676699c58e215cb5f30ddcc06ef170b48bb8317556d517ff40aafd3`.

CI636 did not pass. Its Host partition exceeded the 180-second limit before a final
report was produced. The separate SQL Server/WebKit retirement test host crashed
before publishing either case result. Both hosted WebKit sample journeys passed.
A local unit run exposed a distinct test observation race after a failed sample
callback. The corrected test acquires the existing browser lease before checking
that admission and its ticket are gone. The local coverage run passed 94 Core,
2,133 Host, 5 Architecture and 118 AppHost cases with the existing deadlines.


Independent review tightened the expected subject and disclosure checks for the native
journey. The fixture now sets a verified email on the account that actually signs in
and obtains that account’s subject independently of the consent record. Both providers
returned the exact requested scope in the token response. The consent record, signed
tokens and UserInfo identified the fixture account, and UserInfo omitted its
unrequested email. The two-provider TRX SHA-256 is `f268e3aa566680ba3f4817ff9eb610085b5fcb31ebc37c96d2ff97b5cec9ad17`.

The native journey also retires the registration after successful scoped UserInfo.
The signed token and its corresponding persisted token rows remained valid immediately
after retirement. The issuer denied UserInfo and new authorization for the same client
and callback before the worker ran. The worker then revoked the eligible grants,
preserved history and completed the recorded job. The native and existing confidential
retirement controls both passed on PostgreSQL and SQL Server (four cases, zero skips).
Original TRX SHA-256 `467982fe4be2b9ea2b4ee530209d512e67c6c160f9c03038242fae091378269d`.

CI642’s PostgreSQL consent-policy mutation failed outside its required assertion.
Its report recorded one failed execution, a matching test method and an unknown
error category. It produced no acceptance receipt for that job. The unmodified
consent test passed locally on PostgreSQL and SQL Server. Five fixed diagnostic
labels now identify how far a future execution reaches without retaining account
data, form content or error text.

The native journey now disables the assigned registration, checks that a new
authorization request is refused, re-enables the same client and completes a fresh
scoped sign-in before permanent retirement. Retirement preserved both earlier
lifecycle audit records. The normal native and confidential retirement journeys,
consent tests and diagnostic controls passed locally (14 cases, both providers).
Original TRX SHA-256 `ae0425cb26a8bc83e8c630351584e875f7ae769a6da918749e3df9fb14a0f61a`. The dedicated lifecycle mutation still uses a
confidential client. Public native coverage here comes from the ordinary journey.

CI642’s PostgreSQL managed issuance report contained 71 passing cases and one
failure in the confidential disable/re-enable journey. Its second sign-in to the
independent client received HTTP 400 at MFA, where HTTP 302 was expected. The
case ended 94 ms after a 30-second authenticator interval boundary. The report
did not retain the selected counter or the issuer’s reason for rejection.

The repeated successful-sign-in fixture now prepares an unused synthetic code
that stays valid over the next real counter transition. If its choices are spent
it waits at most one rollover before starting the ceremony. It submits once.
The ordinary replay and boundary fixtures retain their original selection order.
Both provider journeys and both deterministic counter-selection controls passed.
Original TRX SHA-256 `371b24cd209a32d7ce321ae600fa2ec31b53071a2bb6ef48bc5f5c5c9d75524c`.

CI642 also recorded a SQL Server catalogue mutation failure outside its required
assertion. That job produced no acceptance receipt. The PostgreSQL/Chromium
consent case failed before browser creation when Kestrel attempted to listen
on the previously bound BFF port. The original report identifies the address
collision but does not identify the competing listener.

The HTTPS fixture now starts listening when it reserves the loopback socket,
before publishing the origin used to configure clients. Kestrel then receives
that same socket. A Linux check using the compiled fixture verified that a second
listener could not claim the port before host construction, a trusted HTTPS
request succeeded after handoff, and a new listener could claim the port after
host shutdown and disposal.

CI647's Host project exceeded its 180-second deadline. The partial stream
contained 2,125 of 2,135 Host cases, including all 12 added cases. It did
not produce a final Host report. Both hosted local-demo jobs passed in this
run. The earlier CI646 PostgreSQL local-demo navigation failure remains
unexplained.

Across six Host test classes, 31 cases constructed a new pair of 4096-bit
RSA keys to exercise registration, handler selection or request extraction.
These tests now generate separate process-local signing and encryption
templates and import private parameters into independent credentials for
each probe. They retain OpenIddict 7.7.2's RSA sizes and algorithms. A local
covered run of the six classes passed the same 55 case IDs before and after
the change. TRX execution time fell from 21.4 seconds to 4.4 seconds.
The hosted deadline, concurrency settings and assertions remain in force.
The full local covered unit partition then passed all 2,352 required cases
without skips. Host completed its 2,135 cases in 79 seconds of TRX time.

CI648's PostgreSQL/WebKit browser-session job passed 12 of 13 cases. The
local-logout case failed on its first navigation to the BFF sign-in page.
Its protected report did not identify the cause. The same case passed on
SQL Server/WebKit. The cancelled PostgreSQL application mutation job supplied
no archive, and the aggregate coverage job was skipped. [CI648](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/44905368642)

The HTTPS fixture now reports whether it received the first GET to
`/bff/login` and the HTTP status observed in that request's response-starting
callback. Zero means the callback was not observed. These fields are scoped
to one fixture endpoint and survive later sign-ins. They do not establish
that the browser received the response.

CI655 passed all 26 PostgreSQL and SQL Server WebKit browser-session cases.
On each provider the BFF recorded the initial sign-in GET and HTTP 200 at
response startup. The earlier CI648 navigation failure remains unexplained.
CI655 separately failed the PostgreSQL/Chromium dark desktop keyboard case
when it checked focus after an invalid MFA code. The SQL Server/WebKit
application-authority job stopped before testing when the Microsoft Ubuntu
package feed returned HTTP 403. [CI655](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/46075190867)

The keyboard case now observes focus after the error summary is visible and
DOMContentLoaded has fired for the error document. The shared layout defers
login.js, which focuses the summary. In a controlled Chromium 151 page using
the shipped script and the MFA summary markup, the summary was visible before
the script ran and focused at DOMContentLoaded. A non-focusable control stayed
unfocused at that same point. This establishes a possible ordering in the
browser. CI655 did not record when its deferred script ran.


### Browser portion of T002 in verification

The browser profile has local PostgreSQL and SQL Server HTTPS and schema evidence, generated contracts and editor coverage. T002 remains open pending the required acceptance checks. [Verification](verification.md#public-browser-registration) · [Integration guide](quickstart.md#public-browser-client)


### Device polling and managed client key storage under T004 and T005

Both Platform migrations add retained device polling intervals, per-application
public RSA key approvals, and a per-client assertion replay key. A device poll
is bound to a live public device policy and its original revision. The RSA
approval binds both descriptor metadata and the underlying public key material.
Changing the key identifier alone cannot count as a rotation. The replay key
persists across credential revisions. The API registration, verification page
and issuer handlers for these profiles still require integration.

### Confidential machine registration in T003 and T006

The protected registration editor, versioned BFF/API contracts, client-credentials
exchange and service integration example are in verification. Both provider
journeys exercise a signed access token against the shipped orders API. The local
sample substitutes the test issuer address and its discovered public keys.
[Service integration guide](quickstart.md#confidential-machine-client)

### Managed client assertion authentication in T005

The native authentication dispatcher validates signed assertions against a locally
registered public RSA key. The receipt binds the original request, signature,
permanent application, approval and credential revision. The ledger has a global
10,000-row limit. Each successful submission can reclaim up to 100 expired
entries, advancing the affected registrations' observation times before deletion.
Both real database providers accepted exactly one of two concurrent requests
carrying the same valid assertion. Public registration, administrator key
replacement and the final token-publication integration are still open.
