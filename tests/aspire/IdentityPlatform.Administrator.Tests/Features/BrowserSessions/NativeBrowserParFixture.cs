using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Combines genuine fresh password/MFA with explicit B3 native composition over the existing isolated issuer lease.</summary>
internal sealed class NativeBrowserParFixture(AdministratorProtocolScenario scenario, FactorServices browser,
    IssuerContinuationProviderFixture.Attempt attempt, string completion) : IAsyncDisposable
{
    internal const string AttachEvent = "administrator-browser-attach";
    internal const string Attached = "attached";
    private const string OwnershipUnavailable = "Native protocol storage ownership is unavailable.";
    private const string IntentParameter = "admin_login_intent";
    private const string CompletionParameter = "admin_login_completion";
    private const string ProtocolClientActor = "protocol-client";
    private const string AcceptedOutcome = "accepted";
    private const int PasswordAdvanceSeconds = 1;
    internal AdministratorProtocolScenario Scenario => scenario;
    internal JourneyScenario Journey => scenario.Account.Journey;
    internal IServiceProvider Services => browser.Services;

    /// <summary>Keeps the real assertion signer, upgrades through the audited command, and registers the dormant adapter only in the peer root.</summary>
    internal static async Task<NativeBrowserParFixture> CreateAsync(BootstrapFixture fixture, string provider, int completionAdvanceSeconds = PasswordAdvanceSeconds, Action<IServiceCollection>? configure = null)
    {
        var scenario = await AdministratorProtocolScenario.CreateAsync(fixture, provider);
        FactorServices? browser = null;
        try
        {
            Assert.True(await BrowserUpgradeFixture.UpgradeAsync(scenario, BrowserUpgradeFixture.Input(scenario)));
            browser = scenario.CreatePeer(provider, services => { services.AddBrowserCompletionPar(); configure?.Invoke(services); }, scenario.Material.Policy.ForBrowserSessions());
            var attempt = IssuerContinuationProviderFixture.Create();
            var completion = await CompleteAsync(scenario, attempt, completionAdvanceSeconds);
            return new(scenario, browser, attempt, completion);
        }
        catch
        {
            try
            {
                if (browser is not null)
                {
                    await browser.DisposeAsync();
                }
            }
            finally { await scenario.DisposeAsync(); }
            throw;
        }
    }

    /// <summary>Uses the existing enrollment only as the old session; a fresh password and unused native OTP must create the new completion.</summary>
    private static async Task<string> CompleteAsync(AdministratorProtocolScenario scenario,
        IssuerContinuationProviderFixture.Attempt attempt, int advanceSeconds)
    {
        var journey = scenario.Account.Journey;
        Assert.True(await IssuerContinuationProviderFixture.StartAsync(journey, attempt));
        journey.Clock.Now = journey.Clock.Now.AddSeconds(PasswordAdvanceSeconds);
        var password = await IssuerContinuationProviderFixture.BeginAsync(journey, attempt);
        Assert.NotNull(password.Interaction);
        journey.Clock.Now = journey.Clock.Now.AddSeconds(advanceSeconds - PasswordAdvanceSeconds);
        var code = await IssuerContinuationProviderFixture.UnusedCodeAsync(journey);
        var started = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var grant = await IssuerContinuationProviderFixture.CompleteAsync(journey, attempt, password.Interaction, code);
        FactorOracle.RequireWindow(journey.Seed, code, started, valid: true);
        Assert.NotNull(grant);
        await IssuerContinuationProviderFixture.RequireGrantAsync(journey, attempt, password.Interaction, scenario.Account.Grant, grant);
        return grant.BrowserCompletion!;
    }

    /// <summary>Creates a new independently signed assertion on every demand, including completion replay.</summary>
    internal Task<NativeAccountResult> PushAsync(IServiceProvider? services = null, string? assertion = null, Action<HttpContext>? configureHttp = null)
    {
        var fields = NativeAccountFlow.PushFields(scenario, assertion);
        fields.Add(IntentParameter, attempt.Intent);
        fields.Add(CompletionParameter, completion);
        return NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath, fields, services: services ?? Services, configureHttp: configureHttp);
    }

    /// <summary>Reads the committed continuation through a fresh detached database scope.</summary>
    internal Task<IssuerContinuation> RowAsync() => IssuerContinuationProviderFixture.RowAsync(Journey, attempt);

    /// <summary>Hashes only the completion row and its attachment audit; earlier durable assertion consumption is intentionally separate.</summary>
    internal async Task<string> AttachmentSnapshotAsync()
    {
        var row = await RowAsync();
        await using var scope = Journey.CreateScope();
        var audits = await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<SecurityAuditIntent>().AsNoTracking()
            .Where(value => value.Event == AttachEvent && value.CorrelationId == row.CorrelationId)
            .OrderBy(value => value.Id).ToArrayAsync(TestContext.Current.CancellationToken);
        return Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new
        {
            Row = row,
            Audits = audits
        })));
    }

    /// <summary>Proves the replay reached fresh authenticated admission instead of being rejected for assertion reuse.</summary>
    internal async Task<int> AssertionsAsync()
    {
        await using var scope = Journey.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<PlatformDbContext>().Set<AdministratorClientAssertionUse>()
            .CountAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Resolves the returned reference through the native manager, then independently checks its exact persisted ID, deadline and audit.</summary>
    internal async Task<string> RequireAttachedAsync(NativeAccountResult response, IssuerContinuation before, long observation)
    {
        var reference = NativeAccountFlow.ReadReference(response);
        var identifier = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        var row = await RowAsync();
        Assert.Equal(Attached, row.State);
        Assert.True(row.RequestId == identifier && row.CompletionDigest == before.CompletionDigest && row.SessionId == before.SessionId);
        Assert.Equal(before.Revision + 1, row.Revision);
        Assert.Equal(observation, row.ObservedAt);
        Assert.Equal(before.ExpiresAt, row.ExpiresAt);
        await using var scope = Journey.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var token = TestContext.Current.CancellationToken;
        var native = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking().SingleAsync(value => value.Id == identifier, token);
        var profile = await database.Set<AdministratorProtocolProfile>().AsNoTracking().SingleAsync(token);
        var application = await database.Set<OpenIddictEntityFrameworkCoreToken>().AsNoTracking()
            .Where(value => value.Id == identifier).Select(value => value.Application!.Id).SingleAsync(token);
        Assert.True(application == profile.ApplicationId);
        Assert.Equal(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken, native.Type);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, native.Status);
        Assert.False(string.IsNullOrEmpty(native.Payload));
        Assert.True(native.ExpirationDate is { } expires && expires <= DateTimeOffset.FromUnixTimeSeconds(row.ExpiresAt).UtcDateTime &&
            expires <= DateTimeOffset.FromUnixTimeSeconds(row.SessionExpiresAt!.Value).UtcDateTime);
        var audit = await database.Set<SecurityAuditIntent>().AsNoTracking().SingleAsync(value => value.Event == AttachEvent && value.CorrelationId == row.CorrelationId, token);
        Assert.Equal(observation, audit.OccurredAt);
        Assert.True(audit.ActorId == row.ClientId && audit.ActorType == ProtocolClientActor && audit.Outcome == AcceptedOutcome);
        return reference;
    }

    /// <summary>Requires the current finite ownership failure and fully suppressed output, never an arbitrary exception or deadline cancellation.</summary>
    internal static void RequireUnavailable(NativeAccountResult response)
    {
        Assert.False(response.Claimed);
        Assert.False(response.Aborted);
        Assert.False(response.HasCookie);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
        Assert.True(response.Body.Length == 0 && response.Location.Length == 0);
        Assert.Equal(OwnershipUnavailable, Assert.IsType<InvalidOperationException>(response.Failure).Message);
    }

    /// <summary>Disposes the independent native root before the signer, database lease and issuer files it borrows.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            await browser.DisposeAsync();
        }
        finally { await scenario.DisposeAsync(); }
    }
}
