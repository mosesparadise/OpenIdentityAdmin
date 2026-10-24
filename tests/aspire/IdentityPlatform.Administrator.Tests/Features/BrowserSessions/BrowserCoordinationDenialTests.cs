using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises substitutions and exact lifetime boundaries against real protected ticket persistence on both providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class BrowserCoordinationDenialTests(BootstrapFixture fixture)
{
    private const int NextGeneration = 2;
    private const string ProviderTrait = "Provider";
    private const int IntentMinutes = 5;
    private const int ExcessRollbackSeconds = -31;
    private const string MalformedDigest = "invalid";
    private const string SubstitutedSubject = "substituted-subject";
    private const string ForeignClient = "foreign-client";

    /// <summary>Runs each adversarial binding with provider metadata preserved for mandatory native discovery.</summary>
    public static IEnumerable<TheoryDataRow<string, CoordinationFault>> Cases =>
        from provider in IssuerContinuationProviderFixture.Providers
        from fault in Enum.GetValues<CoordinationFault>()
        select new TheoryDataRow<string, CoordinationFault>(provider, fault).WithTrait(ProviderTrait, provider);

    /// <summary>Any wrong binding, spent state, malformed envelope or original-deadline violation leaves all owned rows unchanged.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_ReservedGeneration_When_CallbackBindingOrLifetimeIsInvalid_Then_NoTicketOrAuditIsCreated(string provider, CoordinationFault fault)
    {
        await using var scenario = await BrowserCoordinationFixture.CreateAsync(fixture, provider);
        var browser = BrowserCoordinationFixture.Digest();
        var intent = BrowserCoordinationFixture.Digest();
        var reservation = BrowserCoordinationFixture.Digest();
        var generation = 1L;
        Assert.Equal(generation, await scenario.StartAsync(browser, intent));
        if (fault != CoordinationFault.Pending)
        {
            Assert.True(await scenario.ReserveAsync(browser, intent, generation, reservation));
        }
        var ticket = scenario.Ticket();
        await AlterStateAsync(scenario, browser, intent, generation, ticket, fault);
        browser = fault == CoordinationFault.Browser ? BrowserCoordinationFixture.Digest() : fault == CoordinationFault.Malformed ? MalformedDigest : browser;
        intent = fault == CoordinationFault.Intent ? BrowserCoordinationFixture.Digest() : intent;
        generation += fault == CoordinationFault.Generation ? 1 : 0;
        reservation = fault == CoordinationFault.Reservation ? BrowserCoordinationFixture.Digest() : reservation;
        if (fault is CoordinationFault.Expiry or CoordinationFault.Rollback)
        {
            ticket = scenario.Ticket();
        }
        var before = await scenario.SnapshotAsync();
        Assert.False(await scenario.EstablishAsync(browser, intent, generation, reservation, ticket));
        Assert.True(before == await scenario.SnapshotAsync());
    }

    /// <summary>Changes one persisted state or ticket/time boundary independently of callback lookup substitutions.</summary>
    private static async Task AlterStateAsync(BrowserCoordinationFixture scenario, string browser, string intent, long generation,
        AdminSessionTicket ticket, CoordinationFault fault)
    {
        switch (fault)
        {
            case CoordinationFault.Envelope:
                ticket.ProtectedTicket[0] ^= 1;
                break;
            case CoordinationFault.Actor:
                ticket.Subject = SubstitutedSubject;
                break;
            case CoordinationFault.Expiry:
                scenario.Clock.Set(scenario.Clock.GetUtcNow().AddMinutes(IntentMinutes));
                break;
            case CoordinationFault.Rollback:
                scenario.Clock.Set(scenario.Clock.GetUtcNow().AddSeconds(ExcessRollbackSeconds));
                break;
            case CoordinationFault.Cancelled:
                Assert.Equal(NextGeneration, await scenario.LogoutAsync(browser, generation));
                break;
            case CoordinationFault.Denied:
                Assert.True(await scenario.RunAsync((database, codec) =>
                new BrowserLoginCompletion(database, scenario.Clock, codec).DenyAsync(browser, intent, generation, BrowserCoordinationFixture.Token)));
                break;
            case CoordinationFault.Client:
                await ChangeClientAsync(scenario);
                break;
        }
    }

    /// <summary>Mutates only the fixed-client field through test runtime access to exercise independent admission validation.</summary>
    private static Task<int> ChangeClientAsync(BrowserCoordinationFixture scenario) => scenario.RunAsync((database, _) =>
        database.Set<BrowserLoginIntent>().ExecuteUpdateAsync(update => update.SetProperty(row => row.ClientId, ForeignClient), BrowserCoordinationFixture.Token));
}

/// <summary>Names independently substituted authority inputs rather than sharing implementation predicates with the oracle.</summary>
public enum CoordinationFault
{
    Browser, Intent, Generation, Reservation, Malformed, Envelope, Actor, Expiry, Rollback, Cancelled, Denied, Client, Pending
}
