# Catalogue data and invariants

All additions belong to Platform storage, use existing relational provider contracts
and preserve every historical migration and populated record. Exact generated migration
IDs and runtime grants are recorded after generation and actual execution.

| Entity | State | Ownership and invariants |
| --- | --- | --- |
| ApiAudience | Opaque id, immutable resource identifier, display name, owner label, enabled, positive revision | Product metadata; ordinal unique resource; no credential or second OAuth client |
| ScopePolicy | Native scope id, immutable identity/API kind, enabled, consent text, finite disclosure policy, positive revision | Native scope owns name/resources; only explicitly created managed scopes receive policy |
| Claim policy | None or Email | Email means exactly email/email_verified from current Identity; no raw claim name/expression input; identity scopes only |
| Catalogue deletion job | Opaque job id, kind/target/version, actor, original approval deadline, finite state/result and audit binding | Durable admission; governance-serialized worker rechecks current authority and references; exact one completion/audit, no blanket retry |
| Native scope | Name, display, resources | Written through scope manager in the same owned transaction as policy/audit; API kind exactly one known audience; identity kind no resources |
| Application assignment | Native scope permissions | Explicit operation under application revision; no duplicate authoritative assignment table; all supplied scopes must be managed/enabled |
| Retained decision reference | Interaction id, native scope id and selected audience id | Indexed non-authoritative retention/reference evidence; removed only with eligible terminal interaction cleanup |
| Managed scope decision | Canonical scope/policy binding, requested/granted names, audience and revision identities | Retained with the protected managed interaction/code; current persisted match required at every consuming phase; legacy rows retain old openid-only behavior |

## Validation bounds

Scope names use 1–100 ASCII letters/digits with period, underscore, colon and hyphen,
starting with a letter/digit. Resource identifiers use 1–200 ASCII letters/digits plus
period, underscore, colon, hyphen and slash, starting with a letter/digit. Neither permits
whitespace, quotes, backslash or normalization. Display name/owner maximum200; consent
text maximum500; opaque native id maximum100; search maximum100. These constants are
technical bounds tested independently at equality and one-over, not new authority.
Scope names/resources are ordinal and immutable.
Reject duplicates rather than normalize them away. Reserve special/system names and
case aliases, including fixed administrator scope/audience and offline_access. Openid
remains implicit protocol identity, not a mutable catalogue scope. Email disclosure is
selected only through the finite policy; administrators cannot supply claim expressions.

Default list25/max100, bounded search and stable ordinal ordering; finite scope list
maximum32, finite claim catalogue2. Display and owner fields are bounded plain text,
never rendered as HTML. Versions are opaque kind/id/revision bindings, positive checked
Int64; wrong-kind/id/overflow versions reject. Contract details are finalized in Core
constants and independently tested before HTTP integration.

## Transactions and references

Governance serialization precedes current authority and native/policy reads. Native
manager internal saves participate in the same owned EF transaction as product rows
and one audit intent. Conditional writes verify original revision and exact persisted
post-save state. Foreign key/reference integrity and restrictive runtime grants are
verified on both providers; no HTTP repairs missing state.

Deleting an audience with current native scope-resource or retained-decision audience
references is blocked, including decisions made before a scope was remapped. Deleting a scope
with application permissions or retained native grant/token dependency is blocked.
Assignment/reference creation and deletion use the same ordering boundary. Rejection
never cascades or rewrites existing native data. Disabling policy is separately explicit;
it denies new issuance and current UserInfo but does not erase offline tokens.

## Policy transitions

Create assigns revision1; edit/enable/disable increments once after current-version
validation; delete requires matching version and no references. Assignment increments
application revision and rejects stale version. Policy decisions retain exact granted
scope/resource/disclosure bindings; policy narrowing, mapping or claim expansion cannot
be accepted by an older pending interaction/code. Fresh sign-in and consent are required.
Email values are read from current account when emitted, not trusted from raw principal
claims or duplicated into catalogue rows. Internal policy snapshot fields have no public
token destination. Consent text/display changes must not alter an in-flight decision.

## Snapshot and publication bindings

A versioned bounded canonical decision (maximum16KiB UTF8) plus digest binds requested/
granted names, native scope ids/revisions, audience id/resource/revision and disclosure.
No email values are stored. Nullable legacy snapshot is accepted only for exactopenid
with no resources/extra claims. Indexed retained-decision scope/audience references prevent
deletion while pending or retained native credentials depend on them. Native grant
references and native application permissions also block deletion; revoked/expired
rows count while retained. No private decision digest has a public token destination.

The existing unique interaction AuthorizationId binds a genuine native access token
to its consumed human decision for UserInfo. Extend all row/native snapshots and
prepared-principal checks; current email/source state must match the chosen projection
before publishing. UserInfo requires its own verified bounded read/publication receipt,
not the existing discovery/JWKS-only read claim or an issuance success claim.

| Claim | ID token | Access token | UserInfo |
| --- | --- | --- | --- |
| sub | Required | Required | Exact token subject |
| email | Only consented current Email | Never | Only consented current Email |
| email_verified | Boolean paired with emitted email | Never | Boolean paired with emitted email |
| scope/aud/native metadata | Native protocol rules | Exact granted scope and at most one API audience | Not copied into profile |
| private policy bindings/admin state | Never | Never | Never |

## Durable deletion

DELETE admits a durable job with202 and status URL after normal version/reference/
authority validation. The worker does not acquire new human authority: it rechecks
current actor eligibility/permission and the originally recorded approval deadline,
then version and references under governance. Expired/revoked approval, stale version
or new references terminate safely and require a fresh explicit operator action.
Successful native deletion, product cleanup, sanitized audit and terminal job state
commit atomically. Crash-before-commit is recoverable; committed success is not replayed.
No reference override, cascade, live native token cleanup or automatic reapproval.
