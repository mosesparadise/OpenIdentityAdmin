# Sample state and lifecycle

All state below is private to one Development-only client process; restart invalidates
it. No relational schema or existing provider contract changes.

## Immutable configuration

Client ID 1–100 safe protocol identifier characters; secret 1–256 characters with no
control characters; encoded JSON at most4096bytes, exactly clientId/clientSecret,
no duplicates/unknown fields. One atomically created private run-owned file, no overwrite.
No HTTP mutation endpoint. Read once; malformed input fails closed without echo.

## Native reference state

Maximum128 total retained records (including terminal/expired records); native identifier/reference identifier/status/type, bounded
payload at most32768characters and properties at most8192bytes; timestamps and monotonic
version. Native state lifetime at most5minutes. Creation/update enforce finite limits
under one lock; no eviction of live records. Native manager owns reference-ID hashing.
Every boundary returns detached copies including JsonElement.Clone. Atomic CAS protects
update/delete; terminal statuses cannot revert to valid. Stale mutation throws native
ConcurrencyException. Every create prunes expired records before checking capacity; explicit pruning also
removes old terminal records. Neither path evicts live state. Expired entries remain
within the same hard total cap even before pruning; cancellation is honored.
Native entity caching is disabled. Concurrent native manager redemption must have one winner.

## Browser attempts and sessions

Use random256-bit opaque handles, secure host-only HttpOnly cookies and separate
sample-specific protection. Maximum64 sessions and128 pending attempts; reject capacity,
do not silently evict. Session lifetime cannot exceed the validated access-token expiry
and never slides beyond its original absolute bound. Serialized ticket size bounded64KiB.
Tokens are retained exclusively in server-held tickets. Retrieval is detached; removing
or expiring a ticket is terminal. Renewal of a missing/removed ticket cannot recreate it.

A browser attempt has a random ID and bounded lifetime and is tied to native protected
state/correlation. Logout removes the current session and pending browser authority.
Callback completion must atomically verify the attempt is still eligible before storing
a new session. API/UserInfo requests retain session identity and original deadline;
logout or expiry during the call prevents final response disclosure. Other browser
sessions and shared registration configuration remain intact.

## Projections

Profile: exactly matching sub; optional email and boolean email_verified only as a pair,
with absent email represented by neither. Bound sub256/email256characters and response
4096bytes. Reject duplicate fields, unexpected types, extra sensitive fields or subject
mismatch. API: fixed synthetic order identifiers/status; no arbitrary claims or payload
dump. Views show observation time and current-vs-issuance/expiry limits.

## Publication ordering

Each browser/session has a private asynchronous gate. After external work, publication
admission takes the gate and rechecks presence, identity/version and original deadline.
The gate remains held across bounded final response writing. Logout takes that same
gate: if logout wins, later output is denied; if publication wins, logout waits for
that already-admitted write and then removes the session before reporting success.
Previously sent bytes cannot be withdrawn. Failed/cancelled writes abort the response
and never restore state. Callback session/cookie publication uses the same pending
browser authority gate, so a preempting logout prevents session creation.

Access-token expiry comes from trusted native response lifetime or native access-token
validation, never unvalidated JWT decoding. UI labels configuration as expected audience
until API-native validation establishes the actual resource decision.
