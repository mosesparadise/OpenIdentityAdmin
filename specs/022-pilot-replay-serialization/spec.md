# Feature Specification: Pilot replay serialization

**Branch**: `129-pilot-replay-serialization`
**Created**: 7 November 2026
**Status**: T001–T009 accepted for internal development; [PR126 delivery and exact evidence](acceptance.md).
**Input**: Continue after PR124 delivery with the handoff's initial pilot replay/late-issuance defect.

## Authority and scope

The user's continued roadmap instruction and existing [approval](../../docs/approval.md)
cover correction of the already approved [issuer lifecycle](../006-issuer-lifecycle/spec.md).
This preserves its exactly-one successful concurrent exchange contract; it does not
introduce a policy allowing two otherwise valid concurrent requests to fail merely
because replay overtook issuance. No new product choice is required. Production,
refresh activation, broader client profiles and existing historical/manual/release
gates remain separate. Correction acceptance is recorded separately in [the delivery evidence](acceptance.md); it does not close those separate gates.

## User Scenarios & Testing

### User Story 1 — Finish concurrent exchanges safely (P1)

A pilot client exchanges an authorization code while another request presents the
same code. One otherwise eligible exchange succeeds; the other rejects. After replay
revocation completes, the winning request cannot subsequently create usable token
entries for the same authorization.

**Independent test**: Hold the real winner after durable code consumption, start a
second exchange, observe actual database contention, then release and drain both.
Verify one successful response, one native rejection and the winning access entry
revoked **before any additional replay**. Repeat on each supported provider and with
two distinct issuer processes. Validate the successful tokens independently.

**Acceptance scenarios**:

1. Concurrent eligible requests produce exactly one success and one rejection.
2. Replay waits for the winning issuance to finish; its completed revocation covers
   that issuance. A second replay is unnecessary to reach the required state.
3. An unrelated authorization still exchanges successfully after the contested flow.
4. Wrong client, proof, redirect or native binding cannot cause unrelated revocation.
5. Standalone and mixed-profile hosts provide the same pilot behavior; administrator
   and managed application boundaries retain their own authority and outcomes.

### User Story 2 — Keep consumption durable through failures (P1)

A client whose exchange fails after code consumption must start a new authorization
flow. A failed or lost response cannot restore the consumed code or expose staged
credentials from an uncommitted operation.

**Independent test**: Fail actual token persistence/commit/publication boundaries
after consumption. Observe durable consumption from a fresh context, rollback of
uncommitted generated entries and absence of successful credential output. An
ambiguous commit is not claimed to have rolled back; its response is suppressed and
the original code remains unusable.

**Acceptance scenarios**:

1. Issuance rollback leaves early consumption committed and prevents token output.
2. Missing coordination state, failed acquisition, elapsed deadline, cancellation
   and cleanup failure deny safely, with no repair or automatic exchange retry.
3. Lost success responses require a new authorization flow, as today.
4. A request on another instance cannot overtake an active generation transaction.
   Once that transaction ends, its old continuation cannot commit additional
   generation. Publication/cleanup failure suppresses success without claiming that
   the database lock remains held after commit or connection loss.

### Edge cases

Stale tracked entities; connection loss; commit acknowledgement loss; lock timeout;
callback/body-writer failures; process termination; duplicate/malformed client fields;
expired code while waiting; failed cleanup and retained live resources; schema upgrade
without a pilot account; mixed old/new binaries; deleted coordination state.

## Requirements

- **PRS-001**: Preserve native authentication, proof, lifetime, presenter, subject,
  application and authorization binding before any successful exchange. LIFE-003/008,
  FR-09, NFR-01/08.
- **PRS-002**: Serialize pilot replay and winning issuance across instances; preserve
  exactly one ordinary concurrent success and cover every winning token entry when
  replay completes. LIFE-003/006, FR-17/21/24.
- **PRS-003**: Keep consumption and replay revocation durable independently of later
  issuance failure; never automatically retry an ambiguous security mutation. LIFE-003/005.
- **PRS-004**: Release staged success only after confirmed issuance commit and actual
  required cleanup, within the original request deadline. LIFE-008, NFR-08.
- **PRS-005**: Preserve existing databases, native rows and migration histories; supply
  equivalent fresh/upgrade behavior for both providers. Missing coordination state
  fails closed without runtime repair. LIFE-009, FR-17.
- **PRS-006**: Retain unrelated authorization success, profile isolation and accurate
  evidence; no offline token/session invalidation claim follows from native row
  revocation. LIFE-004/005/009/010.

### Key entities

Existing pilot registration/manifest, account/session, authorization code,
authorization and token entries; independent pilot exchange coordination state;
one original request lifetime and its retained response.

## Success criteria

- **SC-001**: Controlled concurrency has one success, one rejection and no late winning
  issuance after replay completes, without another replay doing the cleanup.
- **SC-002**: Every tested failure after consumption leaves the original code unusable;
  no failed or ambiguous publication returns successful credential output.
- **SC-003**: Fresh and upgraded databases pass the same behavior on both providers,
  including real separate-process contention and unrelated-flow recovery.
- **SC-004**: Existing required cases and coverage floors remain intact; exact-source
  evidence and independent reviews are complete before the defect is closed.

## Assumptions and limits

The fixed Development pilot remains the sole affected client. Its existing request,
transaction, code and token limits remain unchanged. Serializing this bounded pilot
does not establish product-scale throughput or production readiness. The observed
baseline is [feature021's retained reproduction](../021-application-grant-revocation/hosted-verification.md#run534-and-controlled-pilot-replay-correction--6-november-2026).
