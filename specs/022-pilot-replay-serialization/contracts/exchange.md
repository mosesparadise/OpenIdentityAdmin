# Pilot exchange acceptance matrix

The public route, supported grant, Basic authentication, S256, lifetimes and native
protocol error ownership remain unchanged. Healthy otherwise eligible concurrent
exchanges retain exactly one200 response. Replayed codes return native invalid_grant;
resource/commit/cleanup failure never returns successful credentials. Faulted or
expired requests are not counted as ordinary otherwise eligible concurrency.

| ID | Required observation and oracle | Requirements |
| --- | --- | --- |
| PRS-C01 | Genuine password/code flow; winner durably redeemed; real contender blocked in database; winner finishes; replay rejects; all linked winning token entries revoked before another replay. Independent public token validation; unrelated authorization succeeds. | 001/002/006 |
| PRS-C02 | Same ordering across two distinct issuer processes, pinned transport/PID generation proof, both directions and both databases; teardown joins the real work. | 002/006 |
| PRS-C03 | Failure after redemption and after actual token INSERT: original code remains consumed; generated transaction rolls back; no successful response. A subsequent replay remains denied and a fresh flow succeeds. | 003/004 |
| PRS-C04 | Commit acknowledgement loss and actual disposal failure: no success output, no automatic retry/re-exchange; record observed committed state without falsely claiming rollback. | 003/004 |
| PRS-C05 | Acquisition timeout, cancellation, exact request/transaction expiry and missing gate: finite denial, original budget retained, no repair; no additional generation commit after the gate transaction ends. | 004/005 |
| PRS-C06 | Gate/issuance connection loss with competing request and old continuation: gate and all generated entries share the same transaction; no old-generation commit after loss, no replacement transaction/autocommit/context escape and no successful output. Inspect native rows and retain commit ambiguity. | 002/003/004 |
| PRS-C07 | Wrong client/proof/redirect and altered native token/authorization/subject/presenter/manifest bindings; unrelated native state unchanged; authorized original control succeeds where still eligible. Component inputs are not labeled genuine cryptographic HTTP inputs. | 001/006 |
| PRS-C08 | Explicit native fixture establishes revoked code with a valid linked access entry; actual HTTP replay exercises cleanup-write failure with unchanged state, then separate successful cleanup. Preserve12 binding negatives and unaffected authorization; do not present the seeded state as a now-impossible HTTP interleaving. | 001/003/006 |
| PRS-C09 | Fresh schema, upgrade, preserved migration history/native data, repeated Migrator, missing-row denial, least-privilege acquisition and both-provider lock release. No pilot activation during migration. | 005 |
| PRS-C10 | Standalone/mixed pilot routing, malformed and case-aliased transport, administrator/managed isolation, existing native replay/distribution suites, retained browsers and ordinary Aspire demos. | 001/006 |
| PRS-C11 | While a request actually waits for the gate, independently commit account/session/application/scope/authorization restrictions; after release it must observe current authority and deny without generation. Cover original code expiry while waiting without reissue or TTL renewal. Restore reversible eligibility and prove a fresh authorized flow. | 001/006 |
| PRS-C12 | After real native chain revocation commits, fail formatting/callback/publication; no success output and revocation stays committed. Separately lose the generation gate while the consumption child is held: it must never perform late chain revocation, issue credentials or restore a revoked/consumed code to usability. Exercise native bulk revocation that leaves its concurrency token unchanged; assert actual native status as well as replay/output denial, distinguishing any Redeemed-versus-Revoked result. | 002/003/004 |

All requirement numbers above are prefixed PRS-. C01–C09/C11/C12 run on both real providers.
C01/C03–C08 require the common runtime and both standalone/mixed integration entry
paths where applicable; C10 proves that entry-path selection explicitly. C02 uses
the existing real-process lifecycle harness, not two in-process service providers.
Every execution retains exact source/native identities, outcomes and original
deadlines. Mutation controls must prove removal/bypass of the gate, rollback of early
consumption, premature gate release and publication before commit/cleanup are caught.

Offline JWT validation and external application sessions remain unchanged. Revoked
native entries do not erase already delivered tokens from offline consumers.
