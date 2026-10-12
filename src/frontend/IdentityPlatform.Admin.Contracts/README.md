# Admin browser contract tooling

Private build/test package, not a published SDK or React application. `generated/` contains actual host inventories and BFF types; those hosts currently have no management operations. `tests/generated/` contains explicitly non-shipped representative contracts and types. Passing fixture checks does not establish administrator login, authorization, CSRF enforcement or real management-resource acceptance.

Use pinned Node24.19.0/npm11.9.0. Install with `npm ci --ignore-scripts --strict-peer-deps`. `npm run generate` consumes native outputs in repository `artifacts/contracts`; `npm run check` rejects drift and compiles positive/negative consumers. `npm test` proves compatibility controls using pinned oasdiff, installed by `scripts/install_contract_tools.py`. `npm run bundle` creates the test-only consumer used by the trusted-HTTPS browser fixture.

The runtime consumer uses the BFF URL, browser-managed same-origin HttpOnly cookies, explicit antiforgery proof and ETag. Generated types alone provide none of those protections. Never add bearer/access/refresh-token input to this browser client. Actual production protection and documentation access remain M02/M04 work.

Compatibility uses native OpenAPI3.1, Redocly validation, oasdiff1.33.0 with explicit warning failure and external refs disabled, and additional authentication/permission/precondition checks. Dynamic references/anchors and reusable pathItems are rejected until fully supported. Breaking public contracts require a new major; regenerating a baseline does not authorize breaking changes.
