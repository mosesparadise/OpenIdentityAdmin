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


## Device polling and managed key storage

Four local migration cases passed, covering PostgreSQL and SQL Server. They
exercised conflicting writes to the same device window, early and on-time polls,
rollback of the clock, exact expiry, old key rejection after replacement,
same-key relabeling, and repeated assertion IDs across revisions. SQL Server
was tested with a 400-character application ID. Both guard a populated
downgrade and preserve predecessor data across an empty round trip.
These tests exercise storage directly, outside of public protocol endpoints.
