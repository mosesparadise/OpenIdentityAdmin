# Verification quickstart

Use the pinned SDK and repository toolchain; retain configured proxy/TLS trust. From repository root:

```sh
python3 scripts/check-static-analysis.py --self-test
python3 scripts/verify.py unit
python3 scripts/check_assurance.py
python3 scripts/check-ai-instructions.py
git diff --check
```

Run the existing CI provider matrix for PostgreSql and SqlServer with real Aspire resources, HTTP/database and Chromium cases, then the existing coverage aggregate gate. Do not substitute SQLite/InMemory, skip required cases, disable certificate validation or broaden timeouts. The workflow and scripts remain authoritative for flags and locked versions.

Inspect all six Programs, feature Setup calls and the startup compatibility table. Confirm a default/protection-only host still exposes operational routes only; an explicit bootstrap exits without listening; certificate ownership survives configuration/build/verification failures; both Aspire profiles retain dependency and secret ownership. Record results in verification.md, including local platform constraints, exact commit/run and review dispositions. T026 resumes after this separate PR.
