using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Uses real native journeys and fresh persisted witnesses; selectors never enter assertion output.</summary>
internal static class IssuerContinuationProviderFixture
{
    internal const string Login = "Login";
    internal const string Enrollment = "Enrollment";
    internal const string Pending = "pending";
    internal const string Completed = "completed";
    private const string CompleteEvent = "administrator-browser-complete";
    private const string AccountActor = "account";
    internal const string StartEvent = "administrator-browser-start";
    internal const string ProviderTrait = "Provider";
    internal static readonly string[] Providers = ["PostgreSql", "SqlServer"];
    private const int SelectorBytes = 32;
    private const int ClockToleranceSeconds = 30;
    private const int SessionsAfterNewLogin = 2;

    /// <summary>Creates independent test-owned selectors without borrowing the production selector generator.</summary>
    internal static string Selector() => WebEncoders.Base64UrlEncode(RandomNumberGenerator.GetBytes(SelectorBytes));

    /// <summary>Computes the specified decoded-byte digest independently of production continuation hashing.</summary>
    internal static string Digest(string selector) => Convert.ToHexString(SHA256.HashData(WebEncoders.Base64UrlDecode(selector)));

    /// <summary>Allocates the two distinct continuation proofs and independent native browser-budget/correlation values.</summary>
    internal static Attempt Create()
    {
        var result = new Attempt(Selector(), Selector(), CeremonySecrets.Create(), CeremonySecrets.Create());
        Assert.NotNull(result.Request);
        return result;
    }

    /// <summary>Admits through the real service in a separately disposed scope.</summary>
    internal static async Task<bool> StartAsync(JourneyScenario scenario, Attempt attempt)
    {
        await using var scope = scenario.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<IssuerContinuationService>()
            .StartAsync(attempt.Request, TestContext.Current.CancellationToken);
    }

    /// <summary>Always invokes actual password verification with no existing-session substitute.</summary>
    internal static async Task<JourneyPasswordResult> BeginAsync(JourneyScenario scenario, Attempt attempt,
        string purpose = Login, string? password = null, IssuerContinuationRequest? request = null)
    {
        await using var scope = scenario.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<JourneyPassword>().BeginAsync(scenario.Lease.Username,
            password ?? scenario.Lease.Password, purpose, attempt.Budget, attempt.Correlation,
            TestContext.Current.CancellationToken, session: null, continuation: request ?? attempt.Request);
    }

    /// <summary>Calls the unchanged completion overload; persisted binding must enforce B3 even without a caller continuation argument.</summary>
    internal static async Task<JourneyCompletion.Grant?> CompleteAsync(JourneyScenario scenario, Attempt attempt,
        string interaction, string code, string purpose = Login, string? correlation = null)
    {
        await using var scope = scenario.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<JourneyCompletion>().CompleteAsync(interaction,
            correlation ?? attempt.Correlation, purpose, code, TestContext.Current.CancellationToken);
    }

    /// <summary>Reads accepted numeric codes through actual protection, excluding all replay entries from a new native code.</summary>
    internal static async Task<string[]> AcceptedCodesAsync(JourneyScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var rows = await database.Set<AdministratorReplay>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken);
        var protection = scope.ServiceProvider.GetRequiredService<FactorProtection>();
        return rows.Select(row => protection.Unprotect(row.ProtectedCode, row.UserId, row.Generation, FactorProtection.Replay)).ToArray();
    }

    /// <summary>Uses the independent existing RFC6238 oracle with the real native wall clock, not FactorClock.</summary>
    internal static async Task<string> UnusedCodeAsync(JourneyScenario scenario) => FactorOracle.UnusedCode(scenario.Seed,
        (await AcceptedCodesAsync(scenario)).ToHashSet(StringComparer.Ordinal));

    /// <summary>Reads only a detached committed continuation after the operation has fully disposed its transaction.</summary>
    internal static async Task<IssuerContinuation> RowAsync(JourneyScenario scenario, Attempt attempt)
    {
        await using var scope = scenario.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<IssuerContinuation>().AsNoTracking()
            .SingleAsync(row => row.IntentDigest == attempt.Request.IntentDigest, TestContext.Current.CancellationToken);
    }

    /// <summary>Includes continuation, native security, audit and coordination state in a nonsecret rollback witness.</summary>
    internal static async Task<string> SnapshotAsync(JourneyScenario scenario)
    {
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var state = new
        {
            Security = await SecuritySnapshot.ReadAsync(database),
            Continuations = await database.Set<IssuerContinuation>().AsNoTracking().OrderBy(row => row.IntentDigest).ToArrayAsync(token),
            Admission = await database.Set<AdministratorAdmission>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token),
            Browsers = await database.Set<AdministratorBrowserBudget>().AsNoTracking().OrderBy(row => row.Id).ToArrayAsync(token)
        };
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(state)));
    }

    /// <summary>Checks source-session creation and exact persisted proof binding independently of the returned grant.</summary>
    internal static async Task RequireGrantAsync(JourneyScenario scenario, Attempt attempt, string interaction,
        JourneyCompletion.Grant previous, JourneyCompletion.Grant grant)
    {
        var row = await RowAsync(scenario, attempt);
        Assert.NotNull(grant.BrowserCompletion);
        Assert.True(grant.SessionId != previous.SessionId && grant.BrowserCompletion != attempt.Intent && grant.BrowserCompletion != attempt.Browser);
        Assert.True(row.CompletionDigest == Digest(grant.BrowserCompletion));
        Assert.True(row.IntentDigest == Digest(attempt.Intent) && row.BrowserDigest == Digest(attempt.Browser));
        Assert.True(row.InteractionId == interaction && row.SessionId == grant.SessionId && row.UserId == grant.UserId);
        Assert.Equal(Completed, row.State);
        await using var scope = scenario.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var session = await database.Set<AdministratorSession>().AsNoTracking().SingleAsync(value => value.PublicId == grant.SessionId, token);
        var login = await database.Set<AdministratorInteraction>().AsNoTracking().SingleAsync(value => value.PublicId == interaction, token);
        Assert.True(login.BrowserIntentDigest == row.IntentDigest && login.BrowserDigest == row.InteractionBrowserDigest);
        Assert.Equal(login.PasswordVerifiedAt, session.PasswordVerifiedAt);
        Assert.True(session.FactorVerifiedAt >= session.PasswordVerifiedAt && session.CreatedAt >= row.CreatedAt);
        Assert.Equal(session.ExpiresAt, row.SessionExpiresAt);
        Assert.True(row.RetainUntil >= session.ExpiresAt + ClockToleranceSeconds);
        Assert.Equal(SessionsAfterNewLogin, await database.Set<AdministratorSession>().CountAsync(token));
        var audit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(value => value.Event == CompleteEvent, token);
        Assert.Equal(row.CorrelationId, audit.CorrelationId);
        Assert.Equal(AccountActor, audit.ActorType);
        Assert.True(audit.ActorId == session.UserId);
        Assert.Equal(row.ObservedAt, audit.OccurredAt);
    }

    /// <summary>Accepts only the service's finite denial or null grant; unrelated provider failures cannot satisfy a negative oracle.</summary>
    internal static async Task DenyCompletionAsync(Func<Task<JourneyCompletion.Grant?>> action)
    {
        JourneyCompletion.Grant? grant = null;
        var error = await Record.ExceptionAsync(async () => grant = await action());
        Assert.Null(grant);
        if (error is not null)
        {
            Assert.IsType<FactorUnavailableException>(error);
        }
    }

    /// <summary>Keeps proof material in memory without a generated value-printing record implementation.</summary>
    internal sealed class Attempt(string intent, string browser, string budget, string correlation)
    {
        internal string Intent { get; } = intent;
        internal string Browser { get; } = browser;
        internal string Budget { get; } = budget;
        internal string Correlation { get; } = correlation;
        internal IssuerContinuationRequest Request => IssuerContinuationRequest.Read(Intent, Browser)!;
    }
}
