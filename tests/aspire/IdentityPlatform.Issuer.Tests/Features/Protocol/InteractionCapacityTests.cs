using System.Net;
using IdentityPlatform.AuthServer.Features.Accounts.Login;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Protection;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Issuer.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

[Collection("interaction-acceptance")]
public sealed partial class InteractionCapacityTests(IssuerFixture fixture)
{
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Frozen_seven_cookie_starts_admit_exactly_one_and_reject_the_stale_snapshot(string provider)
    {
        var environment = await fixture.GetAsync(provider);
        var cookies = new CookieContainer();
        using var browser = IssuerEnvironment.CreateBrowserClient(cookies);
        for (var index = 0; index < 7; index++)
        {
            await TestLogin.BeginAsync(environment, browser);
        }
        var snapshot = cookies.GetCookieHeader(environment.Issuer);
        var before = await CountsAsync(environment);
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(20));
        var barrier = new InteractionAdmissionBarrier(deadline.Token);
        await using var first = environment.RuntimeServices(barrier);
        await using var second = environment.RuntimeServices(barrier);
        var results = await Task.WhenAll(AdmitAsync(first, snapshot, deadline.Token), AdmitAsync(second, snapshot, deadline.Token));
        Assert.Equal(2, barrier.Arrivals);
        Assert.Equal(1, results.Count(value => value.Interaction is not null));
        Assert.True(results.Where(value => value.Interaction is null).All(value => value.Cookies.Length == 0), "Rejected admission emitted allocation cookies.");
        var after = await CountsAsync(environment);
        Assert.Equal(before.Interactions + 1, after.Interactions);
        Assert.Equal(before.Sessions, after.Sessions);
        Assert.Equal(before.Audits, after.Audits);
        await using var stale = environment.RuntimeServices();
        var denied = await AdmitAsync(stale, snapshot, deadline.Token);
        Assert.True(denied.Interaction is null, "Denied admission or binding exposed unexpected state.");
        Assert.True(denied.Cookies.Length == 0, "Denied admission must not issue cookies.");
        Assert.Equal(after, await CountsAsync(environment));
        var winner = results.Single(value => value.Interaction is not null).Interaction!;
        Assert.Equal(8, await LiveAsync(environment, winner.BudgetId!));
        var budgetOnly = PilotProfile.BudgetCookie + "=" + cookies.GetCookies(environment.Issuer)[PilotProfile.BudgetCookie]!.Value;
        Assert.True((await AdmitAsync(stale, budgetOnly, deadline.Token)).Interaction is null, "Denied admission or binding exposed unexpected state.");
        Assert.Equal(after, await CountsAsync(environment));
    }

    private static async Task<Admission> AdmitAsync(IServiceProvider services, string cookies, CancellationToken cancellationToken,
        string? requestToken = null, long? nativeExpiry = null, DefaultHttpContext? observed = null)
    {
        await using var scope = services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        PilotDatabaseBounds.Apply(database);
        var context = observed ?? new DefaultHttpContext();
        context.RequestServices = scope.ServiceProvider;
        context.RequestAborted = cancellationToken;
        context.Request.Headers.Cookie = cookies;
        var token = requestToken ?? InteractionSecrets.Create();
        var interaction = await scope.ServiceProvider.GetRequiredService<LoginInteractions>().CreateAsync(context,
            token, "urn:synthetic:" + token, nativeExpiry ?? services.GetRequiredService<TimeProvider>().GetUtcNow().ToUnixTimeSeconds() + 300);
        return new Admission(interaction, context.Response.Headers.SetCookie.ToArray());
    }

    private static async Task<State> CountsAsync(IssuerEnvironment environment)
    {
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var cancellationToken = TestContext.Current.CancellationToken;
        return new State(await database.Set<LoginInteraction>().CountAsync(cancellationToken),
            await database.Set<LocalIdentitySession>().CountAsync(cancellationToken),
            await database.Set<SecurityAuditIntent>().CountAsync(cancellationToken));
    }

    private sealed record Admission(LoginInteraction? Interaction, string?[] Cookies);
    private sealed record State(int Interactions, int Sessions, int Audits);
}
