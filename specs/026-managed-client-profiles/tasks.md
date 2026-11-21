# Implementation register

- [ ] T001 Provide managed web integration settings from the authorized registration with a credential placeholder. Verify rendering, callback binding and read-only/terminal exclusions.
- [ ] T002 Add browser and native registration and authorization-code journeys, including exact redirect, separate browser origin policy, S256, and lifecycle regression tests.
- [ ] T003 Deliver the client-credentials protocol and registration together, including a machine-only identity and bounded assigned API scopes.
- [ ] T004 Deliver device registration with verification, approval, polling, throttling, expiry and single redemption on both providers.
- [ ] T005 Implement asymmetric public-key registration, replacement and replay-resistant native client authentication with provider upgrade, concurrency and audit coverage.
- [ ] T006 Extend application integration settings, examples and generated versioned contracts for each delivered profile. Implement permission-aware documentation after resolving viewer activation dependencies.
- [ ] T007 Run required provider, browser, contract, mutation, coverage and supply-chain checks. Independently review the original evidence and record accepted scope in the roadmap.

### Verification for the managed web integration view

T001 is implemented on the feature branch. The UI build, type check, asset
reproducibility check and all 149 existing UI cases passed. The component test
checks the complete settings, multiple callbacks, the secret placeholder and
read-only/retired exclusions. A local Chromium 151.0.7922.173 run of the shipped
bundle used synthetic BFF responses and a 320-pixel viewport. Keyboard opening,
focus, long callback reflow, page-hide clearing and observation expiry passed.
The shared authenticated editor journey now checks the current saved callback,
keyboard disclosure and page-hide removal after actual credential replacement.
The pinned provider/browser matrix and original CI evidence review are pending.

### Profile storage prerequisite for T002–T005

Both Platform provider migrations can now represent public native, browser and device
registrations without credential timestamps or a credential revision. Confidential web
and machine profiles with a shared secret or a public authentication key require a
positive credential revision and an ordered lifetime. Application and issuer eligibility
still require the exact delivered web profile. This increment supplies storage only.

Local Release persistence tests ran 14 cases, seven on each provider. They checked
the profile combinations, partial metadata, alias rejection, populated predecessor
upgrades, native credential preservation, nonempty retirement history, repeated upgrades
and guarded downgrade. A downgrade of a database containing a newer profile fails
atomically. The provider-independent row and schema comparison covers both migration
histories. Original TRX SHA-256 was
`f1f89dc8b59237a5d2bf54d4aa90608cb10c101db876070ce003253f11b8d48c`.

### Native application registration draft

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

The storage follow-up aligns both runtime readiness catalogs with the migrated schema.
All eight focused database cases passed against PostgreSQL and SQL Server on the corrected
storage head. Hosted approval and wider cross-profile, manual and browser acceptance
remain open. PR141's original hosted run failed its contract advisory gate. Its passing
native, browser and mutation evidence is specific to that earlier source.

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
