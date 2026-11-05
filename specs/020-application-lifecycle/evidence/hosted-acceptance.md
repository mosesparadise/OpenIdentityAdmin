# Accepted hosted reconciliation

This is a prose record of the finite verifier reconciliation, not an executable CI input.
Source receipt SHA-256: `7e50cdd33b9bda90fd79f117fee8d014ebedad81f7de60241bd4076a1083c90f`.

```json
{
  "source_commit": "966d93f16ccfb0a2f8097ce208b965a621c2df04",
  "source_tree": "23a62b9dea82ad3ccd9d3635fc5ee193211c0c5d",
  "tested_merge": "700991071391a1964d57aee1feab77656ee4951d",
  "run_id": 41962624583,
  "run_attempt": 1,
  "observations_sha256": "ee20e9e6183730570647087cea98894f6c20ac3165c1d9ac0d9897ee46c083d9",
  "obligations_sha256": "93df35759792ce8de3fcf499e471c45a9e19fe98ead2eec0562a823a96725726",
  "reconciler_sha256": "7f90b2ad03f74e0dd6e92b4469526621a658b3a4e18944e4e8bf7c95ce74dfba",
  "native_visibility_control_steps": 40,
  "native_visibility_controls_per_step": 12,
  "native_visibility_actual_wrapper_steps": 4,
  "jobs": 122,
  "strict_verifier_reports": 101,
  "ordinary_native_executions": 5531,
  "ui_cases": 62,
  "node_contract_identities_checked_by_hosted_verifier": 44,
  "mutation_suite_executions": 16,
  "mutation_jobs": 14,
  "behavioral_kills": 98,
  "coverage_inputs": 98,
  "coverage_modules": [
    {
      "module": "IdentityPlatform.Core",
      "line_percent_rounded": 99.7,
      "branch_percent_rounded": 96.4
    },
    {
      "module": "IdentityPlatform.Infrastructure",
      "line_percent_rounded": 93.3,
      "branch_percent_rounded": 96.1
    },
    {
      "module": "IdentityPlatform.ServiceDefaults",
      "line_percent_rounded": 100.0,
      "branch_percent_rounded": 100.0
    },
    {
      "module": "IdentityPlatform.Migrator",
      "line_percent_rounded": 98.7,
      "branch_percent_rounded": 100.0
    },
    {
      "module": "IdentityPlatform.AuthServer",
      "line_percent_rounded": 94.2,
      "branch_percent_rounded": 98.4
    },
    {
      "module": "IdentityPlatform.Admin.Api",
      "line_percent_rounded": 93.6,
      "branch_percent_rounded": 96.2
    },
    {
      "module": "IdentityPlatform.Admin.Bff",
      "line_percent_rounded": 94.4,
      "branch_percent_rounded": 96.0
    },
    {
      "module": "IdentityPlatform.Worker",
      "line_percent_rounded": 100.0,
      "branch_percent_rounded": 100.0
    },
    {
      "module": "IdentityPlatform.AppHost",
      "line_percent_rounded": 97.1,
      "branch_percent_rounded": 99.0
    },
    {
      "module": "IdentityPlatform.Database.Abstractions",
      "line_percent_rounded": 99.1,
      "branch_percent_rounded": 100.0
    },
    {
      "module": "IdentityPlatform.Database.PostgreSql",
      "line_percent_rounded": 100.0,
      "branch_percent_rounded": 100.0
    },
    {
      "module": "IdentityPlatform.Database.SqlServer",
      "line_percent_rounded": 100.0,
      "branch_percent_rounded": 100.0
    },
    {
      "module": "IdentityPlatform.Database.Hosting",
      "line_percent_rounded": 95.5,
      "branch_percent_rounded": 100.0
    },
    {
      "module": "IdentityPlatform.Database.PostgreSql.Hosting",
      "line_percent_rounded": 100.0,
      "branch_percent_rounded": 100.0
    },
    {
      "module": "IdentityPlatform.Database.SqlServer.Hosting",
      "line_percent_rounded": 100.0,
      "branch_percent_rounded": 100.0
    }
  ],
  "artifact_upload_metadata_matches": 116,
  "limits": "Decoded verifier logs and upload/API metadata reconciled; raw uploaded artifacts were not independently downloaded or reparsed. Coverage percentages are rounded log values; the hosted checker enforced exact thresholds. Semantic requirement/source review remains a separate obligation; this receipt does not close historical, human, Windows, Firefox or public-release gates."
}
```
