# Host and feature composition

The user confirmed this convention for all six production entry points. Preserve the grouped projects/solution folders and existing nested feature hierarchy. The screenshots guide structure; existing error messages, exits, configuration precedence, resource ownership and coverage remain part of the contract.

## ASP.NET Core hosts

`Program.cs` shows host creation, configuration, lifetime and execution. Put hosting extensions in `Composition/WebApplicationExtensions.cs`:

- `AddConfigurations`: apply existing shared hosting defaults, including safe telemetry/health configuration. Keep the framework's default configuration sources and precedence.
- `ConfigureServices`: compose feature registrations explicitly.
- `ConfigureApplication`: compose the ordered middleware and feature endpoint mappings.

Use `Features/Setup.cs` for the project aggregator and `Features/<existing hierarchy>/Setup.cs` for each feature's registration, mapping or middleware. Keep cohesive implementation helpers inside the feature. Use explicit calls; avoid reflection scanning, empty hooks, generic registration frameworks and temporary service providers. Add Contracts, Endpoints, Services or other subfolders only when real contents warrant them.

AuthServer separates accounts/login, issuer protection, pilot activation, protocol handlers and request protection while retaining one shared pilot service-registration path. OpenIddict owns protocol validation/response cryptography. Ordering remains routing → request timeouts → request protection → safe status pages → rate limiting → authentication → authorization → protocol/accounts mapping → health mapping. Preserve `/Features/Accounts` as the Razor root and existing embedded asset names.

The AuthServer startup context is an explicit external credential owner. Program creates it before the application and disposes it after the application. Do not chain `Build().ConfigureApplication()` when configuration can throw while an externally owned resource exists: assign the application to its disposal scope first. A bootstrap command verifies protection and exits before HTTP composition. Keep fixed startup failure categories and operation-local safe error handling; never dump configuration or credentials.

## Native hosts

Aspire uses `Composition/DistributedApplicationExtensions.cs`; feature Setup composes deferred database, service and pilot resources before Build/Run. Preserve named resources, image pins, secret parameters, least-privilege connections and successful-completion dependencies. No empty post-build ConfigureApplication method is needed.

Migrator uses `Composition/HostApplicationExtensions.cs`; feature Setup validates local role settings, registers storage and runs DatabaseUpgrade once. It is a one-shot command with a three-minute deadline, not a hosted HTTP service or background service loop.

## Verification

Keep handwritten Programs/composition inside coverage. Test observable startup, error, lifetime, provider, resource and endpoint behavior instead of asserting method names, folder names or Program line counts. See [feature 005](../../specs/005-host-composition/spec.md), its [startup contracts](../../specs/005-host-composition/contracts/startup.md) and [verification](../../specs/005-host-composition/verification.md). Architecture/test/security/Aspire review and both-provider/browser CI remain required for affected changes.
