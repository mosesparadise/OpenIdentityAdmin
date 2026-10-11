# Lifecycle state and evidence model

No new production schema is proposed for the restart increment. Existing identity/native token stores, LoginInteraction, LocalIdentitySession and SecurityAuditIntent remain authoritative and manager-owned.

Test-only evidence captures resource name, PID plus process start time, container identity, canonical issuer/callback, sanitized timing/status and private in-memory snapshots of the exact old artifact and identity. Compare sensitive values without printing them; do not use records with secret-bearing generated ToString output. Keep raw cookies, codes, verifiers, tokens, private key paths/passwords and database connection data out of reports.

Transition: preexisting genuine state → old process stopped and proven exited → replacement process healthy under same durable identity → original artifact validated/denied under original constraints → explicit fresh recovery if applicable → all generations/resources disposed. Starting a new environment or logging in again is not the original transition.

Cross-node routing evidence and staged/active/retiring credential state will be specified before their respective implementation increments. No production key-state table or activation API is assumed here.
