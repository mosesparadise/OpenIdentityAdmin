# Implementation Plan

Approved scope: A-008. Baseline main `38eb41e06d40b6addbe57e3af839f912645e6dea`, verified remotely on 15 October 2026. Existing branch work is preserved.

Create a vendor-neutral relational contract project and PostgreSQL/SQL Server implementation projects under `src/persistence`. Each runtime provider owns EF setup, immutable schema metadata, model dialect and connection policy; an explicit provisioning capability owns vendor DDL. Shared product code owns authorization, transaction/audit lifetime, validation policy and deadlines. Attach the selected immutable provider to DbContext options so options-only constructors and design-time contexts share the same configuration without global mutable state or a temporary service provider. Include provider identity in model cache identity and reject mismatched EF configuration.

Use explicit instance registration and unique case-insensitive identifiers. Hosts select only registered instances. No assembly scanning, string-to-type activation, boolean vendor dispatch or fallback driver. Provider implementations reference contracts and their driver only; migrations retain assembly names and reference their provider. Runtime migration metadata uses names/IDs without loading executable migration assemblies.

Keep Aspire in separate hosting contracts/adapters; each adapter supplies the existing pinned resources and connection expressions. AppHost owns common secret creation and child wiring. Runtime provider implementation remains sufficient for runtime use; a container adapter is needed only when adding local container orchestration for an engine.

Preserve exact model expressions, collations and migration artifacts. Reuse current integration scenarios; add registration/configuration tests and provider-conformance contracts with independent expected results. Native test discovery and coverage policies must include the new modules and scenarios without weakening current gates.

## Constitution check

Feature-local implementation; no generic repository; managers retain ownership; both providers mandatory; no retry/security weakening; public APIs documented with examples; changed code meets quality limits; independent reviewers before acceptance. No constitution exception proposed.

## Verification and delivery

Record exact source/tree/run and review dispositions in verification.md. Local disk constraints may require remote CI for real database evidence; this is not permission to claim unexecuted tests. Keep a draft PR until exact-head checks/reviews pass, then use authorized normal squash. Resume feature 011 T005 after delivery.
