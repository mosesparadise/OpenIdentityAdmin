# Validate and maintain assurance records

From the repository root, with Python 3.11+:

```sh
python3 scripts/check_assurance.py
python3 scripts/test_assurance.py
```

Expected baseline: all 345 official controls are present, all evidence results are `not_assessed`, and the command explicitly distinguishes integrity from compliance. The test command must pass every positive/negative fixture with no skipped case. Validation requires no network, .NET build, browser or database.

Read `docs/assurance/README.md` for ownership and maintenance. Edit the assessment CSV, not official source wording. Keep local evidence paths relative to the repository root. Do not insert production data, secrets or unreviewed public evidence links. Record feature test IDs and dated evidence only after actual checks. Pending, excluded and excepted rows never become passes automatically.

CI runs this gate alongside the unchanged runtime/provider/coverage gates. Existing product tests are regression evidence, not ASVS control passes.
