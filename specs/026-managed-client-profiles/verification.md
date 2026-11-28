# Managed client profile delivery evidence

## Managed web integration settings

PR141 delivered T001 at squash commit `d69b874a70b0c6fcad49a12584acd37acee2eaea`. CI632 tested source `150bc4c51683e20245e46d045b0e9e923cf911ec` and merge `ab4bec7edf3ac08f818d22cac857fbc52016ac2c`. The squash has the tested tree `521e1d7c83e5bdeb9ac74c8dffe7720ba0b68402`. All 149 jobs passed. The evidence review reconciled 143 original archives against their hosted hashes. [Hosted run](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/44888506086)

The four editor journeys covered PostgreSQL and SQL Server with Chromium and WebKit. They checked keyboard expansion, the application’s exact client ID and current callback, exclusion of the issued secret, and clearing on page hide. Qualified manual accessibility and expanded-content theme assessment remain open.

The original reports contained 6,599 passing native cases, 149 UI cases and 47 contract tooling cases. All 17 modules met the existing coverage floors. The mutation review confirmed 161 causal kills with their baseline and restoration controls. These counts describe the tested source. They do not constitute a benchmark or release approval.

Both documentation viewers remain disabled. The approved internal advisory record retains the two open DOMPurify findings for the embedded Swagger bundle. Viewer activation requires verified patched assets and the remaining documentation activation checks. [Advisory assessment](../../eng/contracts/vendor/advisory-review-2026-11-21.json)

## Client profile storage

PR142 adds provider migrations and readiness checks for the expanded profile and
credential combinations. Existing confidential web records,
credential metadata and retirement history survive the upgrade. A downgrade
with a newer profile present fails atomically on PostgreSQL and SQL Server.

CI633 tested the stacked source `3450dc015a26e9eaf7b29deb8a826afd854c93c1`
at tree `dbc543359653ee51c7052d46680ee05606564dfb`. All 149 jobs passed.
The original persistence reports contain 42 passing cases per provider,
including seven affected cases on each. The managed request-cache expiry case
also passed on each provider. The independent reviews reconciled 143 original
archives, all 17 coverage floors and the 161 mutation controls. [Hosted CI633](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/44888508741)

After PR141 landed, resolving the task-register conflict against main preserved
that tree. CI638 tested source `113eb4822e5f99c76f4ce2f4057e413197e521cc`
at merge `04e379ec226e480d1a0bd6cb991a77197da913c8`. All 149 jobs passed.
PR142 squash-merged as `7461cdd72307b68cb102cb5598d6e88c1571e224`, with the
same tree. [Hosted CI638](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/44894745010)

The final run supplied 143 original archives. Reviewers verified 6,599 passing
native cases, all 84 persistence cases, 149 UI cases, 47 contract tooling cases,
all 17 module coverage floors and 161 causal mutation kills with passing baseline
and restoration controls. The rollback evidence includes a populated public native
row. The retirement history in the migration fixture is synthetic. The storage
increment does not activate additional client types in the issuer.

## Public native registration

PR143 squash-merged as `3453e98d03168acf672be71f4e10907a4fdba77a`. CI656
tested source `175ab8cc5fd361e97c4fe83b37ff5086eb1d51c8` and merge
`0790457a3456ff6acc254e9991d0ff8015009acf`. Both have the squash tree
`25fda44e845086f9a395e462cf8e3cac886a1bf4`. All 149 jobs passed on the
first attempt. [PR143](https://github.com/mosesparadise/OpenIdentityAdmin/pull/143) · [CI656](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/46079293810)

On PostgreSQL and SQL Server, the native journey registered the application
through the BFF, exercised consent and exact callback and verifier checks,
completed the secretless exchange and verified scoped tokens and UserInfo.
It also exercised disablement, re-enablement and permanent retirement with
retained audit history. Device-level callback association is the installed
app owner's responsibility. [Integration guide](quickstart.md)

Review matched all 143 original archives to their hosted sizes and
SHA-256 digests. The reports contained 6,613 passing .NET cases across
123 reports, 150 UI cases and 47 Node tooling cases. All 120 original
coverage inputs reconciled to the merged lines and all 17 module floors
passed. The 25 mutation receipts recorded 161 expected outcomes with
passing baseline and restoration controls.

All 32 Accounts browser cases passed, including all 16 keyboard-enrollment
cases across both databases, both browser engines, light and dark themes,
and compact and desktop widths. The password and MFA error checks observed
actual focus after the deferred page scripts ran. CI655 did not record its
script timing. Its package-feed HTTP 403 and CI648's unclassified navigation
error remain in the run history. [CI655](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/46075190867) · [CI648](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/44905368642)


## Public browser registration

Two real provider upgrade and rollback cases passed. Original TRX SHA-256 was `490e11c67dfb3611c80e259d8ee983bcf6888daf933bc54a59d6db4e72c35105`.

Two provider HTTPS journeys passed. Each registered two public clients through the protected BFF, exercised preflight and discovery, completed human consent and a credential-free S256 exchange, verified user-bound UserInfo, refused a different registered origin, and disabled the client. Original TRX SHA-256 was `95f1f4c8aae712d6fa3cf8ee7dabeea339b7ad8d1d11a0cf420c6696e9585c44`.

All 44 non-browser API contract cases passed. Original TRX SHA-256 was `6d3755245f3b06869815285583983d67b721510c915d072b96e0c7881798cb0e`.

The HTTP journey sends browser Origin headers with cookies disabled. Both cases in the first run ended on the first HTTP 204 when the buffered transport attempted an empty body write. The passing run omits the write for an empty capture. Interactive browser, CI and independent acceptance evidence are recorded separately when available.

CI660 stopped in test selection, mutation source prechecks, static import ordering and the Node contract inventory. The revised matrix accounts for one new HTTPS journey and one new persistence case per provider. Locally, all 93 selection cases and 47 contract tooling cases passed. The revised browser and native journeys passed all four cases on PostgreSQL and SQL Server. Original TRX SHA-256 was `8f075f4a3beadfa46079b91da5846e8736e87b8dfb8fd4c11518015963848aaa`. The broader upgrade suite passed all 16 cases, including guarded downgrade from each immediate predecessor. [CI660](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/46086256328)


CI662 identified a PostgreSQL/WebKit timeout during the sample application
clone journey. The ordinary sample journey passed in that job. The clone
journey also passed on SQL Server/WebKit and PostgreSQL/Chromium at the same
head. The original timeout identifies the clone phase, which includes several
browser actions, without an individual failing call. The safe failure report
now includes both clone source filenames and line numbers. The existing
timeouts and assertions still apply.
[Original job](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/46088920210/job/140805139307)

A separate local Chromium 151.0.7922.173 run used distinct trusted HTTPS origins
for the client and issuer with Playwright 1.63.0. On PostgreSQL and SQL Server,
the browser read discovery and JWKS, completed password, MFA and explicit consent,
exchanged its S256 code and verified the ID token with WebCrypto. The signed
subject and the bearer UserInfo subject matched the independently read account ID.
PostgreSQL passed the first run. SQL Server initially stopped while reading JWKS.
A fresh SQL Server run passed with HTTP 200 for discovery, JWKS, token and UserInfo
and 204 for the UserInfo preflight. The first JWKS failure remains unexplained.
Original TRX SHA-256 was `8c47b17f30ca2b26ac08b0bc55c739c6b46d7276fafca4f3cc6a1ad495b5cb69`.
The observed SQL Server TRX SHA-256 was `511226d7ce85ce2bd77f26a07acedbf39ad3745cf1f1b3d7331e580c69f5fc53`.

CI662's SQL Server confidential lifecycle case ended at 62.184 seconds. The final
fresh token failed strict lifetime validation. Native issuance in this test used a
frozen fixture clock, while the independent validator used real elapsed time with
zero skew. The issued tokens inherit the authorization code's original 60-second
deadline. The PostgreSQL run of the same case passed at 38.672 seconds.

The long lifecycle scenario now uses the existing system-backed fixture clock.
Both provider cases passed locally, including strict lifetime validation, after
this change. Their TRX SHA-256 was `6d2bc492b07461dbb48fc66dea4bf79413aabd5c947af6510d4f555772dd4a36`.
The four local navigation diagnostic controls also passed with system Chromium
151. Their TRX SHA-256 was `b9f021b80adff04ef0883858ace93e76da490b99f726e577557801b86e099b7a`.

## Device polling and managed key storage

Four local migration cases passed, covering PostgreSQL and SQL Server. They
exercised conflicting writes to the same device window, early and on-time polls,
rollback of the clock, exact expiry, old key rejection after replacement,
same-key relabeling, and repeated assertion IDs across revisions. SQL Server
was tested with a 400-character application ID. Both guard a populated
downgrade and preserve predecessor data across an empty round trip.
These tests exercise storage directly, outside of public protocol endpoints.

Two additional provider cases created a real OpenIddict public device application,
an anonymous native authorization, and linked user and device code entries. The
polling store matched the managed and native application, the original scope set,
the two native token types and their common authorization. A poll through the
native reference ID committed a five-second interval increase. A fresh scope saw
that increase and refused the disabled registration. The fixture used a fixed
protocol clock for interval boundaries. Public verification and redemption are
still pending. PostgreSQL and SQL Server TRX SHA-256 was `8e9ecbf6ff10d90e6e91ed548b5c7166d096fa4bb527ef9865a58642094afd91`.

The original client ID, presenter and request scopes are also checked against the
user-code principal. The persisted code entries must be anonymous, and assigned
catalogue scopes are read under the same lock. Both provider cases passed these
checks. TRX SHA-256 was `eac5b93437ef66308eb3dcfd567aae4a39048fd19d3573cdcc56234c4ec98afb`.

### Device authorization code generation

OpenIddict 7.7.2 generated the anonymous device and user codes, their shared authorization, the opaque client references and the verification URI using its installed sign-in dispatcher. PostgreSQL and SQL Server both persisted the original pair under the same transaction as the polling window. The first early poll used the actual returned device code and the committed slow-down was observed through a fresh database scope. Disabling the registration then refused another poll. The fixture captures the native response before HTTP formatting. Human approval and single redemption remain open. Both provider cases passed. Original local TRX SHA-256 `75da3741e1629c664e7bef0af012507bcfb5123e22b2f4521a803c261211e939`.

A further two-provider run used the actual emitted device code in a paced native token request and received `authorization_pending`. A separate native verification request resolved the emitted human code. OpenIddict then committed the refusal, and the next paced token request returned `access_denied`. The cadence transaction and native authentication each had their own database scope under the original poll request. This component check exercises no browser approval or token delivery. Both cases passed with zero skips. Original local TRX SHA-256 `bfe6725a6ee1d32343e8ed8a774e630307cb8b748b993b69ff3d5570202487f6`.

### Native device approval and redemption

Two further tests approved actual emitted user codes after a synthetic account decision. The managed verification handler acquires the governance lock before native user-code redemption and rechecks the stored registration revision, assigned scopes, native code pair and retained window before OpenIddict protects the approved device payload. Approval at 30 seconds retained the original ten-minute expiry. An unrelated advance to the registration observation time left the pending device request usable.

A fresh database scope read each committed device entry and independently checked the original native identity, authorization, authenticated subject, approved status and exact expiry. The linked user entry was redeemed. A paced first exchange produced an access token for that subject. Reuse produced the native token error response `invalid_grant` with no access token. A second approved code was held until its original expiry and obtained the expired polling decision. Once expiry is observed, rolling the clock backwards cannot reopen that window.

The PostgreSQL and SQL Server component cases passed with zero skips. The test boundary begins with a synthetic authenticated subject and ends at the native response. At this component checkpoint, public device enrollment and the HTTPS human verification journey were still outstanding. Original local TRX SHA-256 `b9de9cdb9e9435ffe79f916ea0671bbe8337f2408a7f4c425fb662222363d68b`.

Both cases passed again with the native end-user-verification filter on each approval handler. Other endpoint types do not instantiate the device handlers. Original local TRX SHA-256 `436ce34c5fe3ba9414c5e02cadf127ea37451e992fda48408f1b826e88dc4b3c`.

## Public device verification

Feature 026 T004 covers the device verification portion of FR-02 and roadmap item M07-B. The public entry is `/connect/verify`. The user types the displayed code on the issuer, completes password and MFA, and sees the application name, client ID and scope disclosure before approval or cancellation. The issuer never sends the decision to an application callback. The device uses an independent HTTPS client and retains its original ten-minute code deadline.

`PublicDeviceJourneyTests` starts from a device registration created through the real OpenIddict manager and managed policy. It exercises the shipped Razor forms, rejection, pacing, real token issuance, replay and UserInfo. This earlier run prepared the device registration directly. The administrator enrollment journey below supersedes that setup. The independent browser run used Chromium 151.0.7922.173 and synthetic accounts. Hosted checks, broader browser coverage and release acceptance remain separate milestones.

The Release selection combining `PublicDeviceJourneyTests`, `ManagedJourneyTests` and `BrowserClientJourneyTests` passed 10/10 with PostgreSQL 18 and SQL Server 2022. The device tests exercised identity-only and assigned `email orders.read` requests, verified both token signatures against public discovery keys, checked the resource audience and email destinations, and read the expected subject and consented email from UserInfo. Approved devices redeemed after the five-minute human deadline. The separate device received `access_denied` after cancellation and `invalid_grant` on a second redemption. Original TRX SHA-256 `c1eca96a77b4a5f83fb1577610e5fe185081dbdcaf4cbb042e207c9e5513b77c`.

Four provider upgrade and retention cases passed. They verified the exact predecessor schema, populated downgrade protection, the fourteen-day retention floor and eventual cleanup past a full page of held windows. Both provider models matched their migrations. Original TRX SHA-256 `1f5209a72413fa724622611f15a84ff4ea0d7063e9a493c9875c143c171b91e0`. All 2,135 host tests and 93 selection controls passed. Native test discovery matched all 3,125 reviewed administrator identities. The scenario, documentation, size and literal checks passed. Host TRX SHA-256 `cdee5b8da6da4c1620a44f97622918848bf78bd5cc125f3e25778d451e72898d`.

Independent browser review reproduced an HTTP 400 on both databases when the initial page suppressed the browser’s Origin header. The entry form now uses `strict-origin` and verifies a genuine issuer-origin POST. The corrected Chromium run covered the visible password, MFA and consent pages and one-use redemption from a separate device client. The WebKit binary was unavailable locally after its download returned HTTP 403.

The final scoped public-device HTTPS run passed on both databases, including re-entering the same code in the original browser at consent. Original TRX SHA-256 `c5411d949062c46185d7a3dd0e0a482b17d583e6c3a16e88135124ca586ee586`. The independent browser receipt records the two provider cases, tested source, Chromium version and original TRX digest. The later missing-native-ID guard precedes the lookup and leaves the successful browser path unchanged. [Chromium receipt](evidence/public-device-chromium.json)

## Administrator device enrollment

Feature 026 T004 and T006 cover FR-02, FR-03 and FR-09 through the application editor, the protected v1 endpoints and the issuer. The Release `PublicDeviceJourneyTests` passed on PostgreSQL 18 and SQL Server 2022. Each case created the device through the authenticated BFF, assigned the email and API scopes through the administrator API, renamed it, and used its registered identity for password, fresh MFA, approval, cancellation and one-use redemption. The approved requests verified discovery signatures, audience and UserInfo. The tests also observed rejection of a missing CSRF header, a duplicate client ID, a device callback edit and a stale version.

Disabling the device stopped issuance and enabling it allowed a new sign-in. A second device obtained a pending code pair before permanent retirement. The API retained its application details and job and reserved its ID. The issuer denied further issuance and redemption. The production worker revoked the original user-code entry, device-code entry and authorization on each provider. Original provider TRX SHA-256 `adc815c0ff4af1054f17242a296c181130fde6b23e97b1b63d69a719ac6c446c`.

The 44 .NET contract checks verified the closed two-field registration schema, response details, ETag, access policy, browser CSRF and cache headers. Both production hosts regenerated their default inventories with zero network effects. The generated browser contract and TypeScript passed regeneration, and the contract comparison with the parent reported zero breaking changes. All 47 frontend contract tests, 152 UI tests, 94 core tests and 2,135 host tests passed. Both providers passed the four migration-history checks after the previous draft’s device-verification migration was added to the expected history. This increment requires no schema change.


Four existing callback-profile edit scenarios passed through the bearer API and browser BFF on both providers. The 93 test-selection controls passed, and the native administrator inventory matched all 3,127 reviewed cases, including both device editor browser cases. The repository code-quality checks passed.


The tracked Chromium editor cases passed 2/2 on PostgreSQL and SQL Server. They switched from a callback profile to a device, checked keyboard validation and focus, inspected the actual CSRF-protected create request, and opened the saved client’s device integration settings. They used Debian Chromium 151.0.7922.173 with the local fixture certificate trusted. The tests bind the administrator sign-in to one fixture instant and synchronize the observation clock with the browser before opening the editor. Earlier attempts encountered local certificate trust and timing failures during administrator sign-in. Browser TRX SHA-256 `854d869f6e0613623e40d817fc66181f49c025edf903c004a7f7160f97e86b0a`.
