# Implementation plan: Native Aspire local entry

Branch `110-aspire-local-entry`; approved user correction recorded before code in `docs/approval.md`. Starting main `1d1ce53597d96abe063caf51a0d037f817589a8d`.

## Technical context

The user additionally requested stable .NET10.x SDK selection and latest package updates. Current verification SDK10.0.403/runtime10.0.14 and Aspire13.6.4; see [dependency decisions](dependencies.md). Existing Docker Desktop Linux graph and both provider adapters; no schema change. Main owns edits; independent architecture, Aspire, security and test reviews apply.

## Design

Add an explicit `local-demo` Project launch profile beside existing `https`; IDE startup and `dotnet run --project src/aspire/IdentityPlatform.AppHost --launch-profile local-demo` invoke native Aspire. Keep native development certificate prerequisites and authenticated dashboard; no custom preflight/cleanup script. Normal DCP disposal already has accepted both-provider volume evidence.

Under AppHost Features/LocalDemo add a native resource command, fresh dashboard input dialog and a bounded Docker process adapter. The initializer owns exact container/run labels; inspect only safe identity fields, pin immutable container ID before mutations. Check readiness and ownership independently of UI state, then preserve existing bootstrap and public registration/upgrade operations. Use one per-run atomic gate: pre-mutation cancellation/validation is retryable, any attempted mutation is terminal. Link command cancellation and host shutdown, cap execution and output, drain concurrently, close UTF-8 stdin and kill/join on failure. Stop only the pinned owned initializer on uncertain mutation with a fresh bounded cleanup deadline. Fixed safe results; clear prompt values and byte buffers.

Remove three PowerShell helpers and their Python transport test/CI invocation. Replace with meaningful C# command/process tests; preserve original two LocalDemo runtime identities and change their initialization path to ResourceCommandService with a controlled IInteractionService. Genuine Docker/bootstrap/browser/data/disposal remain real. Do not add test skips or broaden deadlines/floors.

## Constitution check

Approved correction; feature-local composition; native account managers/protocol unchanged; no database migration; least privilege, input secrecy, actual both-provider evidence and independent review retained. No exception requested. Native WithProcessCommand rejected because it logs raw output and lacks the required bounded transport semantics. No application HTTP setup endpoint.

## Delivery

T001 design precedes T002–T004 implementation; T005 integrates real both-provider evidence; T007 dependency/SDK upgrade joins T006 required CI/reviews and normal expected-head squash. Actual Windows/Desktop interaction walkthrough remains open unless executed. M02-C remains the next roadmap item after this correction.
