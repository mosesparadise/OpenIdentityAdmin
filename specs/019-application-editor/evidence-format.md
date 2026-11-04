# Explicit digest records in historical evidence

On 4 November 2026, the unchanged pinned gitleaks8.30.1 source scan reported 92
`generic-api-key` findings in eight feature019 evidence manifests. Every flagged
value was a 64-character SHA-256 digest, keyed by a source or artifact path containing
words such as Token or Secret. No credential value was identified.

The [conversion ledger](evidence-format-ledger.json) names the exact nine JSON map
fields converted into ordered `{ "path": ..., "sha256": ... }` records. All375
path/digest pairs are preserved. Inverting those records reproduces every original
JSON field, value and order, including failed attempts and acceptance limits. The
ledger binds the original Git commit and original/current whole-file hashes. No
runtime mutation-receipt schema, scanner rule, exclusion or allowance was changed.

Historical whole-file references remain intentionally unchanged: the
create-details UI evidence references the original native-issuance and created-client
journey manifests, and the latter references the original native-issuance manifest.
Those hashes resolve to the original bytes at the ledger's `original_commit`; they
must not be silently refreshed to the reformatted representation.

Verification: the full candidate source snapshot passes the pinned scanner with zero
findings. A separate temporary synthetic `clientSecret` assignment still produces
one `generic-api-key` finding and exit1. The clean scan and positive control are both
required to establish this representation repair; the earlier92-finding failure is
retained in the local evidence. Independent evidence review verified the absence of
repository executable consumers requiring the old map shape. This does not close any
other source-quality, dependency, browser, human or release gate.
