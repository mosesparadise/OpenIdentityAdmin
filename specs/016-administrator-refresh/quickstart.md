# Refresh validation quickstart

This is a planning packet. No refresh endpoint or renewable product credential exists yet. The existing [native Aspire local demo](../014-local-b3-demo/quickstart.md) remains the runnable sign-in/logout baseline:

```sh
dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo
```

Use Windows Docker Desktop Linux containers as previously selected; no PowerShell wrapper or WSL terminal. Actual Windows walkthrough remains unperformed. Existing demo proves B3 only, not refresh.

## Checks available now

From the repository root, run the existing assurance/instruction/scope checks and whitespace validation. Locate their actual CLI entry points under `scripts/`; the exact commands/results for this packet are in [verification](verification.md). Spec Kit uses the ignored local `.specify/feature.json` pointer to `specs/016-administrator-refresh`:

```sh
.specify/scripts/bash/check-prerequisites.sh --json --require-spec --require-tasks --include-tasks
git diff --check
```

Do not run fabricated future test filters: T003 discovers and records its own finite native inventory before its characterization is executed; T004 expands and reconciles the full feature inventory before subsequent fixtures are selected. Documentation-only acceptance uses the repository's existing fail-closed selector and visible final gate.

## Planned runtime validation

1. Implement T003's native refresh/revocation characterization with explicit positive, negative and failure controls on both real providers. Confirm installed7.7.2 order and whole-authorization effects before dependent integration.
2. Apply additive migrations to populated prior databases and validate unchanged history/data and second-run identity. Prove BFF runtime has Sessions-only access.
3. Start a genuine issuer plus two BFF processes with one fixed HTTPS browser origin, shared Sessions protection ring and the same configured issuer. Use actual native backchannels and trusted certificates.
4. Perform password/MFA through the actual browser. Confirm credentials exist only as protected server state. Advance time through a supported test clock at owned boundaries (never browser sleeps that mask races); trigger explicit protected renewal after access expiry.
5. Run both simultaneous and adversarial refresh orderings, then response loss/process exit at each recorded boundary. Assert actual sends, native token+authorization rows and local protected state. A new sign-in after terminal failure requires fresh password/MFA.
6. Race logout/replacement with refresh, including unknown successor. Check local denial independently of remote family confirmation and issuer navigation; check unrelated authorization survives.
7. Run current-authority/deadline/scope/key/audit/cleanup/restore negatives, real Chromium/WebKit secrecy/accessibility, contract tests and all required full CI/mutation/coverage checks. Keep browser/human/release gaps explicit.

No production credentials or persistent local databases are used in these disposable fixtures. Tests must dispose actual resources, report bounded sanitized failures and retain the original failed runs. The feature may be enabled in the Development demo only after coherent acceptance; API/React activation remains later work.
