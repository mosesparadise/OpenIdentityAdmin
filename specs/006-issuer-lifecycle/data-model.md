# Lifecycle state and evidence model

No new production schema is proposed for the restart increment. Existing identity/native token stores, LoginInteraction, LocalIdentitySession and SecurityAuditIntent remain authoritative and manager-owned.

Test-only evidence captures resource name, PID plus process start time, container identity, canonical issuer/callback, sanitized timing/status and private in-memory snapshots of the exact old artifact and identity. Compare sensitive values without printing them; do not use records with secret-bearing generated ToString output. Keep raw cookies, codes, verifiers, tokens, private key paths/passwords and database connection data out of reports.

Transition: preexisting genuine state → old process stopped and proven exited → replacement process healthy under same durable identity → original artifact validated/denied under original constraints → explicit fresh recovery if applicable → all generations/resources disposed. Starting a new environment or logging in again is not the original transition.

Cross-node evidence is defined in [contracts/distribution.md](contracts/distribution.md): canonical origin, distinct AuthServer generations, loopback listener port ownership proven by fd socket inodes, immutable selected-node transports, shared native store/material inventory and exact original artifacts. This remains test-only metadata, with no new production schema. Staged/active/retiring credential state follows [the key lifecycle contract](contracts/key-lifecycle.md), with confirmed layouts, passed concrete design review and completed real-host configuration seam. T017 implementation is under verification. No production key-state table or activation API is assumed here.
