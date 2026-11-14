using System.Security.Claims;
using IdentityPlatform.Sample.Client.Features.Sessions;
using Microsoft.AspNetCore.Authentication;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Proves bounded browser/ticket authority, detached credentials and terminal logout across delayed native writes.</summary>
public sealed class SampleSessionTests
{
    private const int TicketMinutes = 2;
    private const int ExpectedBrowserLength = 43;
    private const string PrivateText = "private";
    private const string CallerMutationText = "caller mutation";
    private const int ExtendedTicketMinutes = 3;
    private const int ExpectedAttemptCapacity = 128;
    private const int ExpectedSessionCapacity = 64;
    private const int ExpiredAttemptMinutes = 5;
    private const string SubjectClaim = "sub";
    private const string SyntheticSubjectText = "synthetic-subject";
    private const string AccessTokenName = "access_token";
    private const string SyntheticPrivateTokenText = "synthetic-private-token";

    /// <summary>Unconsumed or ended browser handles cannot mint cookies, including a delayed sign-in after logout won.</summary>
    [Fact]
    public async Task Given_PendingThenEndedBrowser_When_DelayedTicketIsStored_Then_NoAuthorityCanBeCreated()
    {
        var clock = new SampleClock();
        var tickets = new SampleTickets(clock);
        var attempts = new BrowserAttempts(clock, tickets);
        var browser = attempts.Begin();
        var ticket = Ticket(browser, clock.GetUtcNow().AddMinutes(TicketMinutes));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.StoreAsync(ticket));
        using (var lease = await attempts.EnterAsync(browser, TestContext.Current.CancellationToken))
        {
            Assert.NotNull(lease);
            lease.Authenticate(ticket.Properties.ExpiresUtc!.Value);
            var handle = await tickets.StoreAsync(ticket);
            Assert.NotNull(await tickets.RetrieveAsync(handle));
            lease.End();
            Assert.Null(await tickets.RetrieveAsync(handle));
            await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.RenewAsync(handle, ticket));
        }
        Assert.Null(await attempts.EnterAsync(browser, TestContext.Current.CancellationToken));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.StoreAsync(ticket));
    }

    /// <summary>Expiry and serialized ownership stay private despite callers mutating principals/properties or requesting renewal.</summary>
    [Fact]
    public async Task Given_CurrentTicket_When_CopiesMutateOrRenewalExtends_Then_OriginalAuthorityIsPreserved()
    {
        var clock = new SampleClock();
        var tickets = new SampleTickets(clock);
        var ticket = Ticket(new string('a', ExpectedBrowserLength), clock.GetUtcNow().AddMinutes(TicketMinutes));
        tickets.Admit(new string('a', ExpectedBrowserLength), ticket.Properties.ExpiresUtc!.Value);
        var handle = await tickets.StoreAsync(ticket);
        ticket.Properties.Items[PrivateText] = CallerMutationText;
        var copy = await tickets.RetrieveAsync(handle);
        Assert.NotNull(copy);
        Assert.False(copy.Properties.Items.ContainsKey(PrivateText));
        copy.Properties.ExpiresUtc = clock.GetUtcNow().AddMinutes(ExtendedTicketMinutes);
        await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.RenewAsync(handle, copy));
        var unchanged = await tickets.RetrieveAsync(handle);
        Assert.Equal(clock.GetUtcNow().AddMinutes(TicketMinutes), unchanged!.Properties.ExpiresUtc);
        clock.Now += TimeSpan.FromMinutes(TicketMinutes);
        Assert.Null(await tickets.RetrieveAsync(handle));
        await Assert.ThrowsAsync<InvalidOperationException>(() => tickets.RenewAsync(handle, ticket));
    }

    /// <summary>Only one callback may consume an attempt, and logout waits for admitted output without removing another browser.</summary>
    [Fact]
    public async Task Given_AdmittedPublication_When_LogoutWaits_Then_ItOrdersAfterOutputAndLeavesAnotherBrowserAlive()
    {
        var clock = new SampleClock();
        var tickets = new SampleTickets(clock);
        var attempts = new BrowserAttempts(clock, tickets);
        var browser = attempts.Begin();
        var other = attempts.Begin();
        var publishing = await attempts.EnterAsync(browser, TestContext.Current.CancellationToken);
        Assert.NotNull(publishing);
        publishing.Authenticate(clock.GetUtcNow().AddMinutes(TicketMinutes));
        Assert.Throws<InvalidOperationException>(() => publishing.Authenticate(clock.GetUtcNow().AddMinutes(TicketMinutes)));
        var logout = attempts.EnterAsync(browser, TestContext.Current.CancellationToken);
        Assert.False(logout.IsCompleted);
        publishing.Dispose();
        using (var ending = await logout)
        {
            Assert.NotNull(ending);
            ending.End();
        }
        Assert.Null(await attempts.EnterAsync(browser, TestContext.Current.CancellationToken));
        using var second = await attempts.EnterAsync(other, TestContext.Current.CancellationToken);
        Assert.NotNull(second);
        Assert.True(second.Pending);
    }

    /// <summary>Capacity exhaustion never evicts a live attempt or ticket; expiration reclaims bounded admission.</summary>
    [Fact]
    public async Task Given_FullStores_When_NewAdmissionArrives_Then_LiveAuthorityIsNotEvicted()
    {
        var clock = new SampleClock();
        var tickets = new SampleTickets(clock);
        var attempts = new BrowserAttempts(clock, tickets);
        var browsers = Enumerable.Range(0, ExpectedAttemptCapacity).Select(_ => attempts.Begin()).ToArray();
        Assert.Throws<InvalidOperationException>(() => attempts.Begin());
        for (var index = 0; index < ExpectedSessionCapacity; index++)
        {
            using var lease = await attempts.EnterAsync(browsers[index], TestContext.Current.CancellationToken);
            Assert.NotNull(lease);
            lease.Authenticate(clock.GetUtcNow().AddMinutes(TicketMinutes));
            await tickets.StoreAsync(Ticket(browsers[index], lease.Expires));
        }
        using (var overflow = await attempts.EnterAsync(browsers[ExpectedSessionCapacity], TestContext.Current.CancellationToken))
        {
            Assert.NotNull(overflow);
            Assert.Throws<InvalidOperationException>(() => overflow.Authenticate(clock.GetUtcNow().AddMinutes(TicketMinutes)));
            Assert.True(overflow.Pending);
        }
        Assert.NotNull(tickets.FindBrowser(browsers[0]));
        clock.Now += TimeSpan.FromMinutes(ExpiredAttemptMinutes);
        var fresh = attempts.Begin();
        Assert.NotEqual(browsers[0], fresh);
        Assert.Null(tickets.FindBrowser(browsers[0]));
        using var expired = await attempts.EnterAsync(browsers[0], TestContext.Current.CancellationToken);
        Assert.Null(expired);
    }

    /// <summary>A cancelled waiter releases no lease and cannot alter the original publication owner's authority.</summary>
    [Fact]
    public async Task Given_OwnedGate_When_WaiterCancels_Then_CurrentOwnerCanStillEndAndRelease()
    {
        var clock = new SampleClock();
        var attempts = new BrowserAttempts(clock, new SampleTickets(clock));
        var browser = attempts.Begin();
        using var owner = await attempts.EnterAsync(browser, TestContext.Current.CancellationToken);
        Assert.NotNull(owner);
        using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var waiter = attempts.EnterAsync(browser, cancelled.Token);
        await cancelled.CancelAsync();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => waiter);
        owner.End();
        Assert.Null(await attempts.EnterAsync(browser, TestContext.Current.CancellationToken));
    }

    /// <summary>Native ticket removal consumes its creation admission, so a delayed fresh store cannot resurrect it.</summary>
    [Fact]
    [Trait("CriticalDecision", "sample-ticket-removal")]
    public async Task Given_RemovedNativeTicket_When_StoreRepeats_Then_AdmissionIsTerminal()
    {
        var clock = new SampleClock();
        var tickets = new SampleTickets(clock);
        var browser = new string('a', ExpectedBrowserLength);
        var other = new string('b', ExpectedBrowserLength);
        var first = Ticket(browser, clock.GetUtcNow().AddMinutes(TicketMinutes));
        var second = Ticket(other, first.Properties.ExpiresUtc!.Value);
        tickets.Admit(browser, first.Properties.ExpiresUtc.Value);
        tickets.Admit(other, second.Properties.ExpiresUtc!.Value);
        var handle = await tickets.StoreAsync(first);
        var independent = await tickets.StoreAsync(second);
        await tickets.RemoveAsync(handle);
        const string marker = "sample-ticket-removal-bypass";
        string? resurrected = null;
        var failure = await Record.ExceptionAsync(async () => resurrected = await tickets.StoreAsync(first));
        if (failure is null)
        {
            Assert.NotNull(resurrected);
            Assert.NotNull(await tickets.RetrieveAsync(resurrected));
            Assert.Fail(marker);
        }
        Assert.IsType<InvalidOperationException>(failure);
        Assert.Null(await tickets.RetrieveAsync(handle));
        Assert.NotNull(await tickets.RetrieveAsync(independent));
    }

    /// <summary>Constructs a bounded synthetic native ticket with server-only credentials and one browser owner.</summary>
    internal static AuthenticationTicket Ticket(string browser, DateTimeOffset expires)
    {
        var identity = new ClaimsIdentity([new Claim(SampleTickets.BrowserClaim, browser), new Claim(SubjectClaim, SyntheticSubjectText)], SampleTickets.Scheme);
        var properties = new AuthenticationProperties { ExpiresUtc = expires, AllowRefresh = false };
        properties.StoreTokens([new AuthenticationToken { Name = AccessTokenName, Value = SyntheticPrivateTokenText }]);
        return new AuthenticationTicket(new ClaimsPrincipal(identity), properties, SampleTickets.Scheme);
    }
}
