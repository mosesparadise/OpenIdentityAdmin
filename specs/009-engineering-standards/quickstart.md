# Verification guide

Use the SDK pinned in global.json. Existing baseline: `dotnet restore --locked-mode`, `python3 scripts/check-static-analysis.py --self-test`, `python3 scripts/check-ai-instructions.py`.

New quality/CI commands and exact results will be recorded after implementation. Required controls cover clean and invalid scenarios, unknown/mixed/rename change sets, changed legacy members, baseline expansion, expiry, malformed XML, overloads, literal contexts, config validation and dormant production documentation. Do not count planned commands as executed.
