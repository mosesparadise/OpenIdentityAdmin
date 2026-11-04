# Lifecycle requirements review checklist

**Purpose**: Independent reviewer assessment of lifecycle/security/acceptance requirements.
**Created**: 2026-11-04
**Feature**: [spec.md](../spec.md)
**Review ownership**: Reviewer-owned. A checked item means requirements quality is satisfied,
not that implementation exists or passed. Implementation must not change these markers.

## Completeness and consistency

- [x] CHK001 Are disable/re-enable states, same-state behavior, version ordering and credential independence explicit and consistent?
- [x] CHK002 Are current permission, fresh-MFA, system protection and bounded reason/audit requirements specified?
- [x] CHK003 Are stale requests, each human phase, codes and both issuance/disable commit orders covered without promising global revocation?
- [x] CHK004 Are populated upgrades, mixed-version deployment and incompatible rollback limits specified for both providers?
- [x] CHK005 Are UI failure/recovery, late response, keyboard/focus and theme requirements measurable?
- [x] CHK006 Are genuine issuer, provider contention, audit failure, contracts and exact candidate evidence distinguished from inherited passes?
- [ ] CHK007 Are the user-selected bulk-revocation scope and any required durable-job dependencies resolved?

## Notes

Review against spec, plan, data-model, contracts, tasks and quickstart together.
CHK007 remains pending until the actual scope answer is available. Do not record a recommendation
as a user decision. Existing Windows/accessibility/Firefox/A-009/historical/release gates remain open.
