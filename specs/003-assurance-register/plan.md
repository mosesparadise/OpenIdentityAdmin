# Implementation Plan: Assurance Register Baseline

**Branch**: `003-assurance-register` | **Date**: 2026-10-10 | **Spec**: [spec.md](spec.md)

## Summary

Deliver REG-001–006 as a small repository feature: immutable official ASVS CSV plus a separate editable assessment CSV, initial threat/role routing, a standard-library Python integrity gate, seeded regressions and CI. This starts M01-C without pretending the product or deployment has passed assessment.

## Technical Context

- Python 3.11+ standard library; CSV/JSON/Markdown files, no new dependency.
- Linux CI and existing developer Python environments; offline validation after checkout.
- 345 controls across 17 chapters. No network call during validation, secrets or production data.
- `unittest` gate regressions with isolated temporary repositories and a fixed date. Validate the real baseline through the CLI too.
- Existing .NET xUnit v3/MTP, both-provider and coverage jobs remain mandatory and unchanged. This feature changes no runtime source, migrations, product contract or browser UI.
- Gate runs in a separate bounded CI job (five minutes). No claimed product performance result.

## Constitution Check

Pre-design: approved FR-21/22/25 and NFR-01/11/13/14 authorize the scope. All 18 principles reviewed: traceable tests and independent architecture/test/compliance review apply; Features, protocol ownership, current authorization, BFF, both-provider schema semantics, API/theme and Aspire runtime are unchanged. No exception requested.

Post-design: source/control completeness and evidence semantics are explicit; pending applicability cannot pass. All runtime acceptance remains in the roadmap. A-001 only changes certification sequencing. No external action or unsupported assurance claim. Gate passes for this design.

## Project Structure

- `docs/assurance/README.md`: source, states, maintenance, role gaps, known L3 tensions and next work.
- `docs/assurance/catalogue.json`, `asvs-5.0.0.csv`: immutable release provenance/content.
- `docs/assurance/controls.csv`: one assessment record per official control.
- `docs/assurance/threats.json`, `roles.json`: initial boundary/threat routing and accountable roles (named human owners pending).
- `docs/third-party/README.md`: OWASP CC BY-SA attribution, source and unchanged-content notice.
- `scripts/check_assurance.py`, `scripts/test_assurance.py`: strict parser/integrity checker, positive and seeded negative tests.
- `.github/workflows/verify.yml`: dedicated gate with existing pinned checkout; no redundant .NET restore for this job.
- `specs/003-assurance-register/`: specification, quality checklist, research, data model, quickstart, tasks and verification. No external API contract; this is internal repository tooling.
- `docs/roadmap.md`, `docs/README.md`, `README.md`: discoverability, PR #2 handoff and current bounded status.

## Design and verification

Pin the independently acquired official SHA-256/version/tag/commit/URL/counts as reviewed checker constants, separately from editable metadata; keep raw wording outside editable mapping fields. Parse exact headers/row widths, reject duplicates and unsupported statuses, check version/level/id equality and all local task/threat/requirement references. Validate local file evidence stays within the checkout. Dates use ISO calendar dates and are not future-dated; expired exceptions fail. CSV multivalue fields use `|` delimiters; error messages identify the row/control and invariant, never whole record contents.

Every row starts applicability `pending`, result `not_assessed`; provisional triage is routing only. Future `pass` requires applicable status, implementation reference, dated evidence, assessment identity and a documented automated or manual assessment. `not_applicable` requires reviewed rationale/approver/evidence; `exception` requires applicable status, owner, reason, approver, unexpired date and residual risk. Neither exclusion nor exception is a pass. A successful gate proves structure only, never human evidence quality or legal approval.

Tests cover the baseline, altered source, duplicate/missing/unknown identifiers, version/level mismatch, malformed headers/rows/status/date, invalid references/path traversal, unsupported passes/exclusions/exceptions and valid synthetic assessment snapshots. Include coordinated source/manifest replacement, exhaustive status pairs, stale exception metadata, symlink escapes, duplicate JSON keys and canonical date chronology. The test entry point rejects zero executions or any skip. A source-read/network failure cannot be hidden as an empty catalogue. Use actual negative controls; no skip/retry-as-pass.

Dependencies: source pin and spec before data; data model before gate; gate before CI; independent review and evidence before PR. Reviewers inspect independently; the main agent owns changes. No product schema/browser testing additions are needed because those boundaries do not change; existing regression CI still executes.
