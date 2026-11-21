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
