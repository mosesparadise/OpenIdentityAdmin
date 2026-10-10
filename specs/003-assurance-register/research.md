# Research decisions

- **Official source:** ASVS `v5.0.0_release`, commit `5cf9b032440be53ce345ab3c130fda46ba1ce7a2`; official English CSV SHA-256 `98c8fe911b9edb403af8ee05d3ce8201ecac2659e313b053890a62847cdcf680`, 105100 bytes, 345 records. Acquired from the immutable OWASP GitHub path; retain CC BY-SA 4.0 attribution. Prefer the released catalogue over mutable main or remembered identifiers.
- **Separate source and assessment:** retain official CSV byte-for-byte, normalize references as `v5.0.0-10.4.6` while retaining source ID `V10.4.6`. Keep editable assessment fields separately. Source has descriptions, not individual requirement titles; any local labels are summaries.
- **Conservative triage:** all rows begin applicability pending and result not_assessed. Chapter/section routing helps owners find work; it is not a final determination. WebRTC, GraphQL, WebSockets, uploads and deployment controls remain conditional, not silently N/A.
- **Known tensions:** L3 MFA/hardware intent, PAR for every code flow, sender-constrained tokens, asymmetric confidential client authentication, hardware-backed secret management and isolated crypto operations need explicit release implementation/design disposition. The internal password-only pilot does not satisfy them. A-001 does not waive these controls.
- **Source caveat:** v5.0.0-10.6.1 calls response-type values “response mode”. Preserve official text and document the discrepancy; protocol implementation follows normative OAuth/OIDC semantics.
- **Maintenance choice:** small offline standard-library validator rather than a framework, remote spreadsheet or generated pass checklist. Structure cannot verify whether evidence is truthful; independent assessment remains required.

No unresolved clarification affects this bounded baseline. Named owner assignment, final applicability, full OAuth/NIST crosswalk and product evidence remain explicit later work, not inferred approvals.
