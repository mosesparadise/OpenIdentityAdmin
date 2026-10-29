# Data model and ownership

Use existing native OpenIddict application records for sanitized summaries (opaque ID, immutable client ID, display name, client type). No new schema, native writes or migration-history changes are planned.

The existing BFF `AdminSessionTicket.ProtectedTicket` contains authenticated framework token properties holding the exact callback access token and original expiration, bound to its subject/source session, browser generation and original durable lifetime. No refresh token is retained or exercised by this slice. Old status-only tickets remain valid for session observation but cannot call management APIs.

A request-local current-authority observation owns fresh provider context, original deadline and declared lock set; it is not persisted and cannot commit authentication/governance mutations. Native token/authorization managers independently validate stored lifecycle state. No credential values appear in DTOs, logs or evidence.
