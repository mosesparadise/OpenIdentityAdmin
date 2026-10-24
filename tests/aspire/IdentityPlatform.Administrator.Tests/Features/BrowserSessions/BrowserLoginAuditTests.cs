using System.Net;
using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Requires exact real provider permission denial and atomic rollback through product HTTP boundaries.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserLoginAuditTests(BootstrapFixture fixture)
{
    private static readonly string[] ProviderNames = ["PostgreSql", "SqlServer"];
    private static readonly string[] Variants = [Start, Resume, Replacement];
    private const int CleanupSeconds = 10;
    private const string ProviderTrait = "Provider";
    private const string Start = "start";
    private const string Resume = "resume";
    private const string Replacement = "replacement";

    /// <summary>Actual audit INSERT denial prevents start/reserve publication; restored permissions allow the same unspent valid request.</summary>
    [Theory, MemberData(nameof(Failures))]
    public async Task Given_AuditInsertDenied_When_ProductLoginMutates_Then_AllLocalEffectsRollBackAndNoNativeRequestIsSent(string provider, string operation)
    {
        var observation = new BrowserLoginAuditObservation();
        await using var scenario = await BrowserLoginFixture.CreateAsync(fixture, provider, services => services
            .AddDbContext<AdminSessionDbContext>((_, options) => options.AddInterceptors(observation)));
        var token = await scenario.EntryAsync();
        var completion = operation == Resume ? await scenario.CompleteAsync(await scenario.StartAsync(token)) : null;
        if (operation == Replacement)
        {
            _ = await scenario.StartAsync(token);
            _ = await scenario.EstablishAsync();
            token = await scenario.EntryAsync();
        }
        var before = await scenario.SnapshotAsync();
        ExceptionDispatchInfo? primary = null;
        try
        {
            await scenario.Database.Access.SetBrowserAuditAsync(false, TestContext.Current.CancellationToken);
            using var response = completion is null ? await scenario.PostAsync(token) : await scenario.ResumeAsync(completion);
            BrowserLoginTests.RequireDenied(response, HttpStatusCode.ServiceUnavailable);
            Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
            Assert.True(scenario.Database.Access.IsDenied(observation.Failure));
            Assert.Equal(before, await scenario.SnapshotAsync());
            Assert.Equal(0, scenario.Host.Bridge.ParSends);
        }
        catch (Exception error) { primary = ExceptionDispatchInfo.Capture(error); }
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        try
        {
            await scenario.Database.Access.SetBrowserAuditAsync(true, cleanup.Token);
        }
        catch (Exception error) when (primary is not null) { throw new AggregateException(primary.SourceException, error); }
        primary?.Throw();
        using var control = completion is null ? await scenario.PostAsync(token) : await scenario.ResumeAsync(completion);
        Assert.Equal(completion is null ? HttpStatusCode.SeeOther : HttpStatusCode.Redirect, control.StatusCode);
    }

    /// <summary>Discovers both distinct product mutation transactions on both providers.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Failures => from provider in ProviderNames
                                                                         from operation in Variants
                                                                         select new TheoryDataRow<string, string>(provider, operation).WithTrait(ProviderTrait, provider);
}

/// <summary>Captures actual failed-save exception identity without replacing SQL, transactions or runtime credentials.</summary>
internal sealed class BrowserLoginAuditObservation : SaveChangesInterceptor
{
    internal Exception? Failure
    {
        get; private set;
    }

    /// <summary>Retains only the provider exception for exact denial-code assertions, without logging SQL or credentials.</summary>
    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        Failure = eventData.Exception;
        return Task.CompletedTask;
    }
}
