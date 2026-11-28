# Delivery plan

Extend the existing registration model, editor, BFF and API together. `ManagedApplicationProfile` checks the delivered confidential web and native public code profiles, including PKCE and an absent JWKS. Introduce explicit, finite profile-specific descriptor and issuer checks before exposing a new create option. Cover existing records, read-only foreign registrations, cloning, replacement, retirement, jobs and consent. Scope assignment must continue to bind issuance to current catalogue data. Include actual token and resource use for each new profile on PostgreSQL and SQL Server. Browser and native flows need real redirect and PKCE tests. Device and machine registration depend on their corresponding M07 protocol subsets.

Keep assertion validation and token creation in OpenIddict. Bind approval, audit, issuer replay decisions and key replacement to the same client and credential revision. Verify the old assertion/key fails after replacement and the current key works. Validate submitted public key material locally and reject private parameters and remote key references.

Start the integration view with the delivered managed web profile, whose registration, callback and token exchange already have an Aspire sample. Derive its example from the validated BFF detail response. Broader profile examples depend on their actual end-to-end support. Extend authoritative contracts and generated browser bindings with each feature. Reconcile viewer provenance and advisories before exposing authenticated documentation routes.


For the browser profile, persist up to ten independently entered canonical HTTPS origins.
The issuer checks the origin against the current code client before redemption and the
bearer token’s current presenter before UserInfo. Use the origin-first index for
preflight and public discovery. A second enabled client with a different origin
distinguishes a successful preflight from authority to read another client's response.

For T003, authenticate the confidential client through OpenIddict and require each
requested scope to have an enabled API catalogue entry, an enabled audience and an
explicit permission on that registration. Bind the machine subject to the permanent
registration ID and give it a distinct identity type. For T004, use a ten-minute
device transaction and five-second initial polling interval. Bind approval to the
authenticated user, requested scopes and device registration. Preserve the library's
pending, slow-down, denial, expiry and single-redemption behavior on both providers.
For T005, bind the approved public-key fingerprint to the credential revision.
Consume assertion IDs per client across revision changes. Recheck the current key
and credential lifetime before issuing the token.
