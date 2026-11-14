# Sample integration investigation

Baseline: main `91c7070700e8f746e74d9114f881b0005a039b3a`, remotely rechecked
on 14 November 2026. PR134/135 are merged. This is source investigation, not execution.

## Verified boundaries

1. `src/aspire/IdentityPlatform.AppHost/Features/LocalDemo/Setup.cs` composes the explicit
   disposable graph. `DemoContainers.cs` supplies native certificate trust, loopback
   publishing, network aliases and run-owned cleanup. Add sample resources here without
   changing default/production activation or adding custom PowerShell.
2. `src/persistence/IdentityPlatform.Infrastructure/Features/Applications/ManagedApplicationProfile.cs`
   permits managed confidential code/S256 clients with generated secrets and exact
   callbacks, but no post-logout redirects. Existing public integration contracts are
   sufficient for sign-in/local logout; managed remote logout is extra scope.
3. `ManagedTokenTransport` requires `client_secret_basic` and exactly grant_type/code/
   redirect_uri/code_verifier form fields. Native client registration must select that
   method explicitly. Disable PAR for this registration: discovery advertises PAR for
   the distinct administrator profile, not managed-client permission.
4. `ManagedDemand` bounds state/nonce to 512 characters. Preserve native reference-state
   storage and replay protection; disabling token storage and emitting self-contained
   state is not a compatible shortcut. Design an isolated sample-owned state/session
   store and lifecycle, with no reused administrator rows, keys or database authority.
5. The fixed Admin BFF native setup uses private-key authentication/PAR and explicitly
   forbids UserInfo transport. Do not relax its fences or copy its private handlers.
   Reuse narrow supported infrastructure and native client/validation libraries only.
6. Signed RS256 API tokens can be validated using public discovery/JWKS and exact
   issuer/audience/scope with no Platform database or issuer private key. Such validation
   cannot observe immediate revocation. UserInfo is a current human-profile endpoint,
   not arbitrary API authorization or generic introspection.
7. Sample requests require trusted fixed destinations and bounded native transport;
   UserInfo subject must match the validated ID token. Use explicit finite projections,
   never token dumps. Local logout/expiry destroys sample access; no refresh grant.
8. Existing Docker build stages, non-root runtimes, safe ServiceDefaults and shared
   design assets are reuse seams. New hosts need readiness, teardown, telemetry
   redaction, per-module coverage and actual provider/browser evidence.

## Product decisions selected on continuation

- Recommend administrator UI registration then protected local credential entry;
  automatic disposable provisioning changes initializer/audit ownership. The user
  subsequently instructed “Please proceed”; the manual recommendation is selected. A setup screen must have explicit run ownership, authorization and CSRF,
  not an unauthenticated credential-administration endpoint.
- Selected: public-key JWT API validation with visible expiry limits, paired with
  explicit current-state UserInfo. A live-check option needs a supported authority
  contract; do not silently activate introspection or treat profile success as scope
  authorization.

## Review and next design work

Independent architecture source review confirms these boundaries and the need for a
sample-owned native reference-state store. It does not approve pending product choices,
prove runtime behavior or require the user to reapprove routine implementation details.
The continuation resolves clarification. The concrete plan/data-model/contracts now
define storage/lifetimes, dashboard setup, fixed origins, finite contracts and tests.

CI594 belongs to feature023, not this unimplemented sample. CI590 restart and CI592
contrast remain unexplained; CI593 is inconclusive and branch144 must never merge.
No database startup/SQL experiment was run. All external gates remain open.

## Draft test-review dispositions

Independent test review found three precision gaps, corrected in the draft: local
logout now explicitly isolates the current browser from other sessions and shared
configuration; finite email_verified behavior includes false and missing-email cases;
fixed-destination checks cover hostile discovered metadata/JWKS and HTTP redirects.
At that draft-review checkpoint no choice had been selected and no runtime test was
executed. The later continuation above resolves both product choices. Documentation checks
passed: 8 CI-scope tests, 27 assurance tests, instruction/assurance integrity and diff
whitespace. These checks do not validate the unimplemented sample.

## Concrete design review disposition

Architecture review passes for implementation with required lazy-authentication gating,
trusted native lifetime, exact UserInfo projection and logout/publication ordering. Test
review found retained-terminal-state capacity and publication-order ambiguities: the
data model now caps all128 retained states and defines an exclusive publication gate
with explicit logout-first/publish-first outcomes. C08 includes query/form/duplicate/
mixed bearer denials. These are required tests, not claims of existing behavior.
