# T027 — administrator execution and evidence gates

Status: implementation and local verification complete; exact-head CI and squash acceptance pending.

The user authorized T027–T029 on 13 October 2026, with a reviewed and squash-merged PR for each task before the next. The approved MFA design, existing projects and Features hierarchy remain authoritative. No production behavior, package, timeout, coverage floor or quality allowance changes in T027.

## Existing implementation reconciled

T023–T026 introduced the isolated Administrator project, both real provider profiles, six HTTP stories (Bootstrap, Factors, Ceremonies, Accounts, Recovery, Security), and two browser stories (Accounts, Security). Chromium, Firefox and WebKit each execute both browser stories against both providers for affected changes. The strict runner checks the complete native catalogue against the reviewed identities, exact selected display names, successful TRX results and internally consistent result/definition/entry execution bindings. Native discovery UIDs and TRX GUID test IDs are different representations; this gate does not claim direct UID equality or cryptographic artifact provenance.

The accepted baseline has 219 Administrator cases per provider: 207 HTTP and 12 browser cases. Each browser engine executes 7 Accounts and 5 Security cases. There are 12 isolated HTTP provider/story invocations, 12 Administrator browser invocations and 45 mandatory merged coverage inputs across the complete solution. Every nonunit invocation retains its 660-second deadline. Source reviews and seeded controls cover omitted, substituted, duplicated, relabelled, nonpassing and inconsistent evidence. Missing provider/browser prerequisites are failures, never successful skips. Coverage input validation requires every selected nonempty report path; it does not authenticate payload origin or detect a valid report copied between cell paths. Mandatory execution jobs and their strict reports remain separate evidence requirements.

## T027 correction

HTTP invocation preflight previously checked total browser identities and valid story traits, but left per-story counts to browser invocation preflight. An Accounts-to-Security reassignment could therefore pass HTTP preflight while later failing mandatory browser CI. Move exhaustive browser-story manifest/count validation into the shared Administrator catalogue validator. Both HTTP and browser selections now validate every provider and every browser story before executing their selected cases; selected-browser validation still rejects unknown story requests.

Two permanent Given/When/Then tooling cases exercise valid HTTP selection, each provider's story reassignment, and missing/unknown/zero/Boolean/nonexhaustive browser manifests. Before the correction, seven negative-control subcases failed because the invalid catalogue was accepted. Afterward the complete tooling suite and all native discovery selections pass. This strengthens early detection; the previous full browser CI was already required and is not retroactively declared passing for malformed input.

The shared TRX validator also previously inspected only the first Execution child of a test definition. It now requires exactly one, rejecting identical or contradictory extra bindings. A permanent Given/When/Then report regression failed both duplicate variants before the fix and passes afterward. No native UID conversion is reimplemented. The complete Python tooling suite now passes 133 cases.

## Traceability and acceptance

ADMIN-009/011 and MFA-01–13 depend on complete provider/browser evidence; the gates support FR-22/24 and the approved testing obligations. Meaningful behavioral coverage remains T028, not a consequence of inventory counts. T029 owns final review/runbook convergence. Human accessibility/password-manager observations, external assessment and certification remain distinct obligations.

Local native discovery found 438 identities and validated all 12 HTTP plus 4 browser provider/story selections (engine selection is a separate runtime dimension). The 11-item Spec Kit requirements checklist is satisfied; no extension hooks are configured. Independent review and final CI details will be recorded in the PR and verification record before task acceptance.
