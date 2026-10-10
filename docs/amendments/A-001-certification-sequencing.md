# Certification Sequencing Amendment A-001

Status: Approved by the requesting user on 10 October 2026: “Approve the sequencing amendment.” This amends version 0.2 only as specified below.

## Confirmed compatibility finding

The official OpenID conformance-suite release-v5.3.2, commit `11999aad62ec292f36101410d9d5b761d44903ff`, builds ordinary Basic OP authorization-code requests without a PKCE challenge and code exchanges without a verifier. Only a dedicated positive PKCE test adds both. The Basic plan exposes no supported all-tests PKCE setting. Consequently, its unmodified Basic OP plan conflicts with the approved requirement to require PKCE for authorization-code requests.

This finding comes from static source inspection; a conformance plan has not yet been run. Config OP does not perform the affected authorization flow and is not blocked by this particular mismatch. Config OP alone does not satisfy the planned authentication certification.

Sources: [Basic plan](https://gitlab.com/openid/conformance-suite/-/blob/11999aad62ec292f36101410d9d5b761d44903ff/src/main/java/net/openid/conformance/openid/OIDCCBasicTestPlan.java#L27), [authorization request construction](https://gitlab.com/openid/conformance-suite/-/blob/11999aad62ec292f36101410d9d5b761d44903ff/src/main/java/net/openid/conformance/openid/AbstractOIDCCServerTest.java#L513), [token request construction](https://gitlab.com/openid/conformance-suite/-/blob/11999aad62ec292f36101410d9d5b761d44903ff/src/main/java/net/openid/conformance/openid/AbstractOIDCCServerTest.java#L629), [dedicated PKCE test](https://gitlab.com/openid/conformance-suite/-/blob/11999aad62ec292f36101410d9d5b761d44903ff/src/main/java/net/openid/conformance/openid/OIDCCEnsureRequestWithValidPkceSucceeds.java#L18), [skipped tests cannot certify](https://gitlab.com/openid/conformance-suite/-/blob/11999aad62ec292f36101410d9d5b761d44903ff/src/main/java/net/openid/conformance/condition/client/ConfigurationRequestsTestIsSkipped.java#L10).

## Recommended change

Move resolution of the Basic OP suite mismatch from the exit criteria of the first implementation milestone to an independently tracked **external certification dependency**. Allow protocol and subsequent product implementation to proceed using mandatory S256 PKCE, the product's full positive/negative tests and applicable unmodified conformance plans. Record Basic OP as blocked until an official supported mechanism or suite change is accepted for certification.

Retain the approved public general-availability gate: Basic OP and Config OP certification for the actual declared configuration must be completed before the release is marketed as the approved certified production release. If the dependency remains unresolved, the software remains an internal/pre-release candidate; public launch requires resolution or a separate explicit scope amendment.

Do not add certification-client exemptions, disable PKCE, inject missing protocol fields through a proxy, skip required tests, report expected failures as passes, or certify a relaxed configuration as evidence for the hardened one. PAR remains required for the Admin BFF as approved, not silently imposed or relaxed globally to manipulate test outcomes.

## Exact document effect

- In the Spec Kit plan, milestone 001 exits with enforced and tested PKCE, a recorded suite-compatibility finding and an owned external blocker, rather than requiring resolution of that external blocker before product work continues.
- In the assurance plan, the first-milestone compatibility check becomes a required investigation/evidence gate. Its unresolved result blocks Basic OP submission and the certified public release, while implementation milestones can proceed.
- FR-25, mandatory PKCE, the ASVS baseline, independent assessment and public-release certification requirements remain unchanged.

## Alternative

Keep the original first-milestone exit gate. Continue only work independent of issuer acceptance until an officially accepted suite mechanism is available. Contacting the Foundation or publishing an upstream issue requires separate explicit authorization; no external communication has been sent.

## Decision record

Approved on 2026-10-10. Mandatory PKCE and the public-release certification gate are retained. Implementation milestones may proceed while Basic OP suite compatibility is tracked as an external dependency.
