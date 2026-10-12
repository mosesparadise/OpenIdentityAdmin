# Contract dependency review boundaries

`npm-review.json` binds the private development toolchain's exact lock bytes, license expressions and known notice obligations. CI rejects lock/identity/license/source drift and nonzero current npm audit findings. npm aliases are checked using the installed package's actual name. This is not a legal or certification statement.

`vendor/` records exact NuGet archives, embedded JavaScript/npm byte identity, upstream commits and preserved notices. CI rejects archive/version/notice changes without review. These records do **not** establish a complete bundled-component SBOM: Swagger's published source map does not consistently map to its accompanying JavaScript, and its inferred component list is provisional. Unknown versions must never be reported as vulnerability-free.

## Mandatory documentation activation and public-release gate

Before any production call to `MapDocumentation`:

- Establish a reliable complete component inventory for the actual delivered Scalar/Swagger bytes; resolve the stale Swagger map and unknown/candidate versions.
- Reassess every current advisory against those bytes and configuration, using patched assets where applicable; retain specific evidence for any not-affected conclusion. A different version/hash/configuration invalidates a previous disposition. npm overrides do not modify NuGet-embedded JavaScript.
- Complete third-party notice/source/license obligations, including unpackaged toolchain notices before redistributing the tooling.
- Complete M02/M04 authentication, authorization, same-origin proxy and real-contract acceptance. Protect HTML, JSON and all static assets before Scalar's anonymous asset metadata is reached.

The production Admin.Api and Admin.Bff pipelines remain health-only. Synthetic fixture success, known-component advisory queries and exact archive hashes cannot satisfy this activation gate. The foundation's purpose is to make the boundary and remaining evidence explicit, not to approve production viewer deployment.

The [12 October presentation-configuration reassessment](vendor/configuration-review-2026-10-12.json) preserves only the prior specific source-bound advisory dispositions after reviewing encoded title/label options and extracted policy files. It does not resolve vendor inventory, notice or activation gates. Runtime regression results are recorded separately in feature 009.
