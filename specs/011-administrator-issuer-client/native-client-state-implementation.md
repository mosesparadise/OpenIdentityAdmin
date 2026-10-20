# Native client state creation and cryptographic input binding

## Scope and contract

This increment continues the approved T005–T009 batch and its coherent B2 dependencies (FR-005–011/013, planned B2-CLIENT-01/02/04 and B2-PROOF-04). It supplies explicit, dormant native client-state creation and strengthens actual issuer/client cryptographic generation. It does not complete T005, T019–022, B2 acceptance, product callback/cookie activation or production release.

`DurableClientState` retains the installed `GenerateLoginStateToken` slot, original principal and native nested dispatcher. A fresh Sessions transaction owns the existing client-state coordination row. Actual native row creation, signing/encryption and opaque-reference payload persistence must all finish; authoritative row reconciliation, exact capacity reconciliation, confirmed commit, transaction cleanup and actual child disposal precede releasing state to later native PAR handlers. The private observer is then removed so later client assertion generation retains its own native pipeline.

The hard limits are 1,024 live and 10,000 retained native rows, with a maximum 300-second prepared-state lifetime and strict expiry. Unknown rows consume capacity. Cleanup considers at most 100 old expired candidates, applies native case-insensitive state-type semantics consistently in memory, rechecks exact row predicates, and never deletes related or live rows. This uses the existing additive Sessions coordination migration; no existing database or migration history changes.

## Review corrections

Review found that a frozen principal and matching persisted payload did not bind the separate `SecurityTokenDescriptor` actually passed to native crypto. Shared `ProtocolTokenPreparation` and `ProtocolTokenDescriptorSnapshot` now observe the real native credential, subject and metadata handlers in order. They require the initial default descriptor, exact installed handler and configured credential identities, expected encryption, and unchanged descriptor fields between stages. The issuer's existing active-key selection is permitted only around that actual policy call. Code/PAR/state remain encrypted; the fixed profile's access/identity tokens retain their intended policy.

Prepared issuer/client principal binding includes ordered identities, authentication/name/role/actor semantics and claims/properties, allowing only the native row identifier addition. Flattened-claim identity repartition cannot change the subject selected by native signing. Client continuation binding also freezes custom parameters, state-generation/inclusion flags, hints, audiences/resources and the registration PAR setting through real child cleanup.

The state verifier retains native manager reference hashing/ordinal filtering with entity caching disabled and additional filtering enabled. After all quota and manager operations, one final untracked scalar query verifies payload, reference, dates, status, subject and absence of application/authorization links. No database-capable lookup follows that snapshot before final frozen-input/time checks and commit.

The changed active-key policy source now documents its public contract and names its existing lifetime bounds; five superseded legacy quality allowances were removed. No allowance was widened or renewed.

## Concrete tests and local evidence — 18 October 2026

- All 978 Host cases pass, zero skips (56.146 seconds), including 50 new state/binding/descriptor controls and existing native issuer composition/key controls. An actual native protected-state cryptographic round trip verifies decryption/signature validity and original claims. Late descriptor mutations cover subject, claims, headers, dates, type, audience, compression, credentials and handler identity.
- Persistence and administrator projects build with zero warnings/errors. Local database tests were not executed.
- The new persistence case on each provider exercises actual encrypted reference-state creation, fresh-request native reference/decryption/signature validation, pre-crypto descriptor changes, post-commit cleanup substitution, and direct same-count persisted payload/reference drift during the final quota query. Every fault must be reached and committed row counts are independently checked.
- Two independently composed clients contend for the last live slot. The fixture holds the real coordination UPDATE and requires provider DMV evidence of the exact peer session blocked by that transaction before release. Both operations and observer cleanup are joined. Retained equality/over-limit and bounded mixed-case/foreign-row cleanup are also checked.
- The new administrator case on each provider uses genuine account login and native PAR before seven actual code-generation mutations. Each requires attack reach, suppressed output, rollback of new issuance and retained durable earlier consumption.
- Actual portable administrator discovery is 1,123 cases: all 1,121 prior names/type/traits preserved, two additions. Mandatory Persistence counts increase from seven to eight per provider; ProtocolAdmission increases from 50 to 51. All existing partitions, deadlines, coverage floors and mutation suites remain required.
- All 206 Python selection/evidence controls pass. Independent architecture and security source reviews pass after correcting the fixture's explicit `bff` table qualification. Hosted runtime and aggregate coverage evidence for this increment remain pending; source review and local pure tests are not both-provider acceptance.

## First hosted state checkpoint — 18 October 2026

Source `b7927f8df32e7835b47810f2370b0e02dda6dc8e`, tree `0ec021e6128f6585df3c1a519363401fddeeb55e`, was tested through merge `59f323d1fbe3579c27dd70519c49f5a3b6ccd36b` in [run 39098680196](https://github.com/mosesparadise/OpenIdentityAdmin/actions/runs/39098680196). Independently retained raw unit evidence passes 1,073/1,073 (978 Host), preserving all 1,023 previous identities/traits and adding 50, with zero skips/errors. Unit artifact `12125304763` has SHA-256 `414f2078b9bf78f4bbe43e6ffb5602b0a2aae0143c8733c6f8740aa38d449a4d`.

The foundation prerequisite failed before database execution: the separate portable persistence inventory omitted the two new NativeClientState cases even though aggregate counts had increased. Actual local native discovery preserves all 14 prior names/types/traits and adds those two cases. The corrected inventory passes the production complete-inventory/catalogue/selection checks at eight cases per provider. Static analysis also found one unused import in `NativeGenerationFault`; it is removed. These are prerequisite corrections, not proof of the unexecuted database cases. New exact-head hosted evidence remains required.

Before superseding that run, independent review retained both 51-case admission reports and both 131-case issuer-flow reports: all 364 pass, including both new native generation attack cases. Combined with units, 1,437 raw cases pass; all 1,385 prior non-foundation identities/traits are preserved, plus 52 additions. This establishes server generation regression evidence, while client-state creation/quota evidence remains unexecuted behind discovery. The preceding initializer source `831f8eb` completed all 65 hosted jobs and independently verified all 1,429 raw cases, 50 coverage inputs and 15 module floors; it does not certify this newer source.

## Remaining coherent B2 work

Complete actual native-client composition with durable separate credentials, strict metadata/PAR/private_key_jwt interoperability and test-owned callback transport; remaining two-instance callback/key/response-loss/failure evidence; all required authority/registration/quota race and mutation controls; operator runbook; and independent exact-head acceptance. Preserve the provider refactor and prior ownership deliveries. Keep all six historical findings, 48 human rows and activation/public-release blockers open. Firefox remains deferred only through internal B2 under A-007 and must return before B3 implementation/activation/release.
