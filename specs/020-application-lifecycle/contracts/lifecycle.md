# Proposed lifecycle contracts

## API and BFF

Admin bearer routes use `/api/v1/applications/{id}/disable` and
`/api/v1/applications/{id}/enable`, both POST. The BFF exposes the corresponding fixed
versioned application routes under its existing browser contract. Do not accept an arbitrary
upstream URL, desired boolean in the ordinary edit DTO, or forwarded credential/header bag.

Both actions require:
- Current `applications.disable` and persisted MFA freshness within the existing five-minute
  bound, checked under original operation authority. Neither read/write nor credential permission
  substitutes. Existing API/BFF session/source/expiry checks remain independently necessary.
- One canonical `If-Match` for the exact application aggregate. No wildcard/list/weak ETags.
- JSON `{ "reason": "maintenance", "confirm": true }`. Reason is one of `maintenance`,
  `security`, `configuration`; exact lowercase, maximum 16 characters. Confirmation must be true.
- Existing bounded JSON transport, content type, same-origin and anti-forgery generation checks.
  Retain the existing application request-body maximum; do not enlarge it for these small inputs.
  Reject unexpected fields, duplicate fields and a `revokeGrants` field in the issuance-only scope.

Successful 200 returns the sanitized application details and new ETag, no credential output,
with `Cache-Control: no-store`. Extend details with `enabled: boolean | null`: true/false for
supported managed policy, null when lifecycle is not managed. Keep `editable` separate.
Lifecycle state is shown on details in this slice; adding list filters is not required.

Errors preserve the existing finite ProblemDetails mapping: 400 invalid input, 401 invalid/expired
authority, 403 permission/fresh-MFA/system denial, 404 missing target, 409 unsupported/same-state,
412 stale precondition, 428 missing precondition, and existing sanitized unavailable mapping.
Validate version before same-state comparison. Do not echo reason/input or private upstream errors.
Cancellation/unknown commit outcome is not a successful response or a retry instruction.

## UI

Details show Enabled/Disabled independently of credential expiry. Explicit Disable/Re-enable
opens an inline confirmation with accessible heading, reason selection and consequence text;
cancel returns focus to the initiating action. Keep existing responsive light/dark tokens.
Saving disables duplicate submission. Success replaces details/version using the accepted
response. Conflict offers reload; expiry/permission loss clears actionable state; unknown outcome
instructs the operator to reload before another deliberate action. No automatic mutation retry.

Text before disable must explain: new sign-ins and token issuance stop; existing application
sessions and offline-validated tokens may continue; this action does not revoke stored grants.
Text before re-enable explains that new sign-ins become eligible, old attempts stay invalid,
and expired credentials still require separate replacement. Never claim global logout.

## Compatibility and scope

Add both operations and nullable lifecycle projection through the authoritative API/BFF contracts,
then regenerate TypeScript/browser schema using the repository's existing generator and exact
compatibility assertions. Preserve protocol contracts and existing editor/credential response
semantics. Direct-call negative tests remain required even if UI hides an action.

The grant-revocation preference remains pending. If included, specify a separately authorized
durable job with 202/status/progress/idempotent processing and failure semantics; do not treat
`applications.disable` as `grants.revoke` or silently add a synchronous scan.
