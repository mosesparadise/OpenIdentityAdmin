# Sample client/API work status

Preparation and proposed delivery boundaries; refine via pinned plan/tasks/analyze
after the two choices in [spec.md](spec.md). No runtime implementation is complete.

- [x] T001 Verify remote baseline, preserve work and inspect real Aspire/managed-client/
  BFF/API boundaries; obtain independent architecture source investigation.
- [ ] T002 Resolve setup and API-authority choices; finalize the specification.
- [ ] T003 Complete plan, threat boundaries, state/data model, route contracts,
  walkthrough and requirement/test matrix; independent design review and analysis.
- [ ] T004 Implement isolated configuration, native callback protection and server-held
  session/credential lifecycle with replay/expiry/negative regression tests.
- [ ] T005 Deliver sample sign-in, consent results, current UserInfo and local logout
  UI with accessible light/dark/error states and no credential disclosure.
- [ ] T006 Implement protected synthetic API/sample call under the selected authority
  contract, including exact token-kind/audience/scope denial tests.
- [ ] T007 Compose ordinary Aspire resources, TLS trust, readiness and disposable
  teardown while preserving existing administrator behavior.
- [ ] T008 Verify the real integration on both providers/retained browsers; include
  lifecycle/failure/secrecy, concurrent callback consumption, logout racing response
  publication, signing-key rotation/discovery outage, partial-startup cleanup, native
  inventory, module floors and mutation evidence.
- [ ] T009 Reconcile final evidence, complete independent reviews, update roadmap/
  handoff/walkthrough and deliver through normal reviewed squash.

Dependencies: T001→T002→T003→T004; T005/T006 follow the approved T004 boundaries;
T007 composes implemented hosts; T008 verifies them; T009 follows all required checks.
Main owns edits/integration; reviewers are independent. All external gates remain open.
