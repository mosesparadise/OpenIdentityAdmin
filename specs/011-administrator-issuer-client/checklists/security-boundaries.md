# Requirements review: B2 protocol boundaries

Reviewer-owned requirements-quality checklist. A check means the requirement is adequately specified, not that runtime behavior passed. Feature: [spec](../spec.md), [contract](../contracts/administrator-protocol.md).

- [x] CHK001 Are pilot/admin/disabled composition and named authentication boundaries explicit?
- [x] CHK002 Is current full session/permission admission distinguished from earlier cookie validation and reusable authority?
- [x] CHK003 Are freshness, source expiry, race ordering and native principal bindings precise?
- [x] CHK004 Are fixed registration, exact-match initialization and audited no-repair semantics specified?
- [x] CHK005 Are assertion audience/type/algorithm/time/jti and cross-endpoint replay obligations distinct from native code/state replay?
- [x] CHK006 Are mandatory PAR/S256 and metadata/method downgrade denials explicit?
- [x] CHK007 Are separate assertion/state/B1 key roles, durable state and native correlation/nonce checks specified?
- [x] CHK008 Are native retry defaults, lost-response behavior and exact request-count evidence covered?
- [x] CHK009 Are capacity/cleanup, cancellation, ambiguous commit, key loss and restore consequences bounded?
- [x] CHK010 Are both-provider native, concurrency, mutation and coverage obligations mapped to tasks?
- [x] CHK011 Are test-owned callback evidence and later B3/browser/API/refresh activation clearly separated?
- [x] CHK012 Are historical findings, human assessment, assurance and expired-approval limits preserved?


Independent architecture/test/API and protocol/security reviewers evaluated all 12 items as PASS after the storage-budget and issuance-ordering corrections. The main agent records their dispositions here; this is requirements-quality review only. See [verification](../verification.md).
