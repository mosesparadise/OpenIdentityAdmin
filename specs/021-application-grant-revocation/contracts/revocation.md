# Application grant job contracts

Admin API `/api/v1`; browser BFF `/bff/api/v1`. Protocol routes remain unchanged.

| Route | Input / current authority | Result |
| --- | --- | --- |
| POST `/applications/{id}/grant-revocations` | Exact current If-Match; canonical Idempotency-Key UUID; `{reason,confirm:true}`; current grants.revoke and fresh MFA | 202, bounded job projection and Location status URL after durable commit/cleanup. |
| GET `/applications/{id}/grant-revocations/{jobId}` | Current applications.read and grants.revoke; no MFA freshness requirement | 200 bounded projection; exact app/job binding; no-store. |
| GET `/applications/{id}/grant-revocations` | Same current read permissions; no arbitrary filters | Bounded latest 10 jobs in stable newest-first order, enough to recover navigation/reload; no total-count scan. |
| POST `/applications/{id}/grant-revocations/{jobId}/continue` | Fresh current application If-Match and job version in body; new canonical Idempotency-Key; confirmed finite reason; current grants.revoke and fresh MFA | 202 same job with new effective approval, only from stopped state. |

Requests are at most32768 UTF-8 bytes with depth16; bounded response projections are
at most524288 bytes with depth16. Reject limit+1 and duplicate/unknown JSON members.
Counts are canonical nonnegative decimal strings (maximum19digits, Int64 range),
never JavaScript numbers; test safe-integer boundaries and Int64 maximum.

Projection: canonical job ID, application ID, opaque job version, state, finite failure
category, committed authorization/token counts and created/updated timestamps. No token,
subject inventory, native payload, actor credential material, exception text or arbitrary
reason. Status reads require current access on every call. No exact percentage unless
an actual complete denominator exists; first slice displays counts and state.

Invalid/unsupported input is 400; no current authentication 401; missing permission/fresh
MFA or protected target 403; missing/mismatched job/application 404; enabled target,
incomplete-job collision, key/hash conflict or expired retained key 409; stale target/job
version 412; missing preconditions 428. Stale preconditions win over same-state errors,
except an authorized exact idempotency replay returns its recorded acceptance without
re-executing a mutation. Database/owned deadline failures are sanitized 503; ambiguous
success is recovered explicitly by the same scoped key under current access.

BFF uses existing exact-origin native antiforgery and protected current session generation,
server-held bearer forwarding, finite input/output and original publication deadlines.
No browser OAuth tokens or generic proxy routes. Strict OpenAPI/DTO schemas and generated
browser types change together. Header allowlists explicitly include the scoped key.

Application details shows Revoke grants only with current revocation ability and a disabled
managed target. Confirmation names the target, explains disabled-until-success and offline
limits, and requires a reason. Pending/partial/stopped/completed states are distinct;
recovery is explicit and refreshes stale versions before fresh confirmation. Re-enable
reports a bounded conflict while any incomplete job remains, including stopped work.
Automatic polling is read-only, finite and cancelled on navigation/session expiry.
