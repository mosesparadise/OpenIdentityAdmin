# Feature Specification: Explicit host and feature composition

**Feature Branch**: `005-host-composition`
**Created**: 2026-10-10
**Status**: Implemented and independently reviewed; PR #8 awaiting review, not merged
**Input**: User confirmed squash PR #7 first, refactor all six production entry points before feature 004 T026, preserve grouped projects and nested features, use Composition extensions and feature Setup classes, and preserve startup/coverage behavior.

## User Scenarios & Testing

### US1 — Understand and extend a host (P1)

A maintainer can find host wiring separately from each feature's registration and endpoints, while retaining the established project layout.

**Independent test**: Review the six entry points and trace each existing registration and endpoint through explicit composition calls; run the existing host and architecture tests.

1. Given an existing production entry point, when a maintainer reads it, then it describes host creation, configuration, lifetime and execution; registration and pipeline details reside in extensions.
2. Given an existing nested feature, when extending its registration or endpoint mapping, then its Setup owns that wiring and the project's Features/Setup explicitly composes it.

### US2 — Preserve operational and security behavior (P1)

An operator can use the same configuration and commands with the same health, error, cleanup and security behavior.

**Independent test**: Real process startup/failure tests, certificate ownership tests and both-provider migration/Aspire/protocol/browser suites.

1. Given valid or invalid settings, when starting a host, then configuration precedence, fixed safe errors, exit codes and enabled routes remain unchanged.
2. Given one-shot migration or pilot initialization, when it completes or fails, then it retains its deadline and exit contract and does not become a long-running HTTP service.
3. Given externally loaded credentials, when configuration/build/startup/run exits, then the application/provider is disposed before those credentials, including early failures.
4. Given either supported local database, when starting Aspire, then resource names, secret recipients, least-privilege connections and successful-completion dependencies remain unchanged.

### US3 — Review and continue delivery (P2)

The owner can review a separate refactor PR and see the remaining feature work.

**Independent test**: The roadmap, task list and verification record distinguish completed work from unrun gates; PR title is a Conventional Commit.

1. Given PR #7 is merged, when this refactor is proposed, then it is a separate PR and feature 004 T026–T030 remain pending.
2. Given new source locations, when verification runs, then handwritten composition remains subject to all nine existing module coverage floors and no required test is skipped.

### Edge cases

Invalid database settings before credential loading; certificate registration/build/verification failure; disabled pilot with explicit initialization; valid credentials with pilot enabled outside Development; initialization failure without a listener; dependency startup failure; shutdown/cancellation; diagnostic redaction and configuration reload; default/protection-only health mode; Razor and embedded asset discovery.

## Requirements

- **HC-001**: Apply the confirmed thin Program/composition convention to AuthServer, Admin.Api, Admin.Bff, Worker, Migrator and AppHost, adapting builder/application types for the last two.
- **HC-002**: Preserve grouped projects and nested features; explicit Features/Setup and feature Setup calls own wiring. Add subfolders only for actual contents.
- **HC-003**: Preserve default configuration sources/precedence, safe errors, exit codes, command flags and deadlines. Do not introduce bootstrap logging or Program coverage exclusions.
- **HC-004**: Preserve certificate ownership and disposal on all exits; do not create temporary service providers or static mutable startup state.
- **HC-005**: Preserve middleware order, named schemes, native OpenIddict validation/cryptography, rate/time/form limits, protocol paths, Razor root and embedded assets, including disabled routes in health-only modes.
- **HC-006**: Preserve Aspire resource/secret/dependency contracts, database pins and provider parity; Migrator remains the only schema-writing entry point and runs once.
- **HC-007**: Retain meaningful regression, both-provider and browser verification with all existing per-module coverage floors; record actual evidence and independent review dispositions.
- **HC-008**: Publish the documented change as its own Conventional Commit PR before T026; preserve the visible remaining roadmap and squash merge convention.

## Success Criteria

- **SC-001**: All six production entry points satisfy the confirmed composition convention, with every existing registration and route accounted for in review.
- **SC-002**: Every HC requirement maps to tasks and evidence; all seven existing CI gates pass with zero skipped required cases and all nine modules meeting their current coverage floors.
- **SC-003**: The owner can locate the refactor's plan, task status, evidence and next unfinished item from the roadmap.

## Confirmed decisions and dependencies

These are the user's explicit choices, not inferred architecture preferences. Screenshots guide structure only. No database schema, UI design, authentication policy, dependency version, solution grouping or release scope change is planned. PR #7 was squash-merged as `9c52c2d1720d072385404ec6e640aae6e6cabac3`. Existing v0.2 approval and the current instruction authorize this refactor; the new PR is for review, not automatic merge. This feature supports PRD FR-21/22/24 and NFR-08/11/14; it preserves existing FR-07/09/15/17/18 controls without claiming certification or assessing new ASVS rows.
