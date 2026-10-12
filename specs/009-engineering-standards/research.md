# Research and decisions

- Existing CI runs costly suites on prose changes. Decision: fail-closed path classifier plus final aggregate gate; retain manual/nightly full checks. Rejected workflow paths-ignore and skip markers because they can leave required checks pending or suppress relevant policy checks.
- Existing test identity is consumed by exact manifests, filters and mutation checks. Decision: migrate scenario names and all consumers together, retaining assertions and historical evidence; no cosmetic automated prefix-only names.
- Existing code has extensive undocumented methods and literal candidates. User approved incremental retrofit, exact legacy inventory and immediate new/changed enforcement. Rejected a repository-wide unreviewable rewrite and silent blanket suppressions.
- Dedicated Roslyn tooling approved. Syntax awareness avoids treating comments/string contents as executable code; semantic meaning and reuse still need review. Seed alias, overload, partial-type, multiline and literal-context cases.
- Descriptive OpenAPI metadata is hardcoded in two host-local Setup classes. Separate deployable settings from fixed supported contracts/security policy. A discovered fixture-description override means a hostile renderer probe is currently missing; correct the setup and explicitly verify probe presence.
