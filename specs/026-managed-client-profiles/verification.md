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

## Native managed client assertion consumption

PostgreSQL and SQL Server each accepted exactly one of two concurrent native
authentication requests carrying the same signed client assertion. Each test
created a local RSA public-key registration with the real application manager,
used its private key only in the test client, and dispatched native token-endpoint
authentication. The losing request received `invalid_client`. Independent
operations also verified the signed bytes against the registered public key and
checked the persisted receipt, fingerprint, time bounds and sequential duplicate.
Both provider cases passed. Original TRX SHA-256 was `504cdfd8237941a9a256486a04cc55c237639fdb74e1b968dfce6703499e72d4`.
The composed provider test covers authentication. The key-management HTTP surface
and authorization of the resulting access-token response remain separate work.

The internal management operations now register confidential web and machine
clients directly with a public key. They generate no shared secret. The API
operation requires current write and credential permissions and recent MFA on
registration. Replacement requires credential permission, recent MFA, confirmation
and the current application version. The provider authority cases passed. They
checked stale versions, loss of credential permission and the MFA expiry boundary.
The operations have no versioned HTTP, BFF or editor entry point yet.

Both databases retained an audit detail for each registration and replacement.
The committed detail links its audit ID to the previous and resulting credential
revisions and the public-key fingerprint. The old signing key and a spent
assertion ID under the replacement key were refused. A fresh signed assertion
succeeded. The replay store reclaimed expired entries for the same client and
accepted fresh proof after the retention cutoff. Downgrade protects populated
key audit history. Eight provider migration and compatibility cases passed.

| Local evidence | Original TRX SHA-256 |
| --- | --- |
| PostgreSQL and SQL Server API mutation authority | `824ea13fce2f81dd4720c8ec981c02c4ad9ae5f5085e366a56621dea5052fe0e` |
| Committed rotation history and credential use | `070fc3f19fa4201d418426de6496e1a3ec1116b008f5c80c7c0f2a2cf8f66891` |
| Provider migration and compatibility cases | `be431df7c76926178979db1937dff94952215ef162f771151a5b4943134e9430` |

### Keyed machine registration and token issuance

Separate real HTTPS runs registered a synthetic service through the versioned bearer API and through the cookie-authenticated BFF. Both PostgreSQL and SQL Server accepted a signed client assertion, issued only the assigned machine token, and served the Orders sample with that token. Both routes recovered the current ETag via GET. Rotation refused the previous key and refused a previously consumed identifier under the replacement. The replacement could sign a fresh request. Permanent retirement accepted the keyed application, retained its native configuration and linked revocation job, and prevented fresh issuance.

A scoped test advanced the clock by 61 seconds while the original HTTP parent disposed, after the native token was committed. Both providers withheld the response at the assertion deadline. The independent token and registration lifetimes still exceeded that deadline. The test used the real dispatcher, credential storage, response transfer and sample API.

All 44 non-browser contract cases, 47 Node contract tooling cases, 152 UI cases and 93 test selection cases passed. The generated browser contract documents cookie authentication, antiforgery and a strong ETag for rotation. Key registration requires both grants and recent persisted MFA. The editor handles only public RSA parameters and uses key-specific integration settings.

Original local reports

- `keyed-machine-retirement.trx` — SHA-256 `5ed6b3e346a213ffb7ef2500c65640fb9e509585e897d47fd812216373c3cb0d`.
- `keyed-machine-browser-lifecycle.trx` — SHA-256 `010129ee70b83853994f02371982b15b89a451c85305a3584c373b8850f60c62`.
- `key-browser-contracts.trx` — SHA-256 `88c8c1ad5bf67762d5e67481da6cadba0d13faef74354ce2886cb67babe2d602`.

### Device authorization code generation

OpenIddict 7.7.2 generated the anonymous device and user codes, their shared authorization, the opaque client references and the verification URI using its installed sign-in dispatcher. PostgreSQL and SQL Server both persisted the original pair under the same transaction as the polling window. The first early poll used the actual returned device code and the committed slow-down was observed through a fresh database scope. Disabling the registration then refused another poll. The fixture captures the native response before HTTP formatting. Human approval and single redemption remain open. Both provider cases passed. Original local TRX SHA-256 `75da3741e1629c664e7bef0af012507bcfb5123e22b2f4521a803c261211e939`.

After integrating the device PR, both provider upgrade cases passed with device generation and key assertions on the same owned native dispatcher. The device request now runs under its actual parent storage request. Original local TRX SHA-256 `c235dc9b2cfaf89e4c5dafde0810fa66ad3cd97b267155d8ef22d65b505ab2c9`.

### Public key registration in the browser editor

An independent real-browser run covered PostgreSQL and SQL Server with system Chromium 151.0.7922.173 and Playwright .NET 1.63.0. On each provider the administrator completed password and MFA sign-in, focused the validation summary for a missing key expiry, registered a public RSA-3072 key, confirmed replacement, and reopened the saved application. Both mutations included the session antiforgery value and only public key material. Replacement supplied the original strong ETag and returned revision two. Details showed the new expiry and the service's RS256 authentication settings.

An initial observation failed after both registrations returned 201 when the probe tried to inspect a no-store response. The second run observed the rendered editor, submitted request headers and persisted details. Both provider cases passed without skips. The tested application and administrator fixture sources matched commit `19fbe1bf`. Original passing TRX SHA-256 `16a5dfd6e6822f4bd7b5d5af6b9d3fc7c503c0722d06bb89c9c259a74d4120d9`. Independent receipt SHA-256 `d84661e3015ee8fdd53f7d4505ef17eb660b8baf0d674dc38686964bc57ca623`.
