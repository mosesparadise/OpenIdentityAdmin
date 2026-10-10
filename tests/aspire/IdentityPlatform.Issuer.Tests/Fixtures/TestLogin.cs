using System.Net;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

public sealed class TestLogin
{
    public required TestFlow Flow
    {
        get; init;
    }
    public required string Interaction
    {
        get; init;
    }
    public required string Antiforgery
    {
        get; init;
    }

    public static async Task<TestLogin> BeginAsync(IssuerEnvironment environment, HttpClient browser, string? prompt = null, string? maximumAge = null, bool exchange = false)
    {
        var flow = environment.RelyingParty.Prepare(exchange: exchange, prompt: prompt, maximumAge: maximumAge);
        using var response = await browser.GetAsync(environment.RelyingParty.StartUri(flow), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/account/login", response.RequestMessage!.RequestUri!.AbsolutePath);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        flow.InteractionId = IssuerEnvironment.InputValue(html, "Interaction");
        return new TestLogin
        {
            Flow = flow,
            Interaction = flow.InteractionId,
            Antiforgery = IssuerEnvironment.InputValue(html, "__RequestVerificationToken")
        };
    }

    public async Task<Uri> CompleteAsync(IssuerEnvironment environment, HttpClient browser)
    {
        using var response = await SubmitAsync(environment, browser);
        Assert.True(response.IsSuccessStatusCode && response.RequestMessage!.RequestUri!.AbsolutePath == "/account/complete",
            "The genuine protected form did not complete.");
        await using var scope = environment.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await database.Set<LoginInteraction>().AsNoTracking().SingleAsync(value => value.Id == Interaction, TestContext.Current.CancellationToken);
        Assert.True(row.CompletedSessionId is not null, "Completion did not persist a session binding.");
        await environment.RecordSessionAsync(browser, Flow, row.CompletedSessionId);
        return environment.Continuation(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
    }

    public async Task FollowAsync(IssuerEnvironment environment, HttpClient browser, Uri continuation)
    {
        Assert.True(continuation.GetLeftPart(UriPartial.Authority) == environment.Issuer.GetLeftPart(UriPartial.Authority), "Unexpected continuation origin.");
        using var response = await browser.GetAsync(continuation, TestContext.Current.CancellationToken);
        Assert.True(response.IsSuccessStatusCode, "The genuine callback failed.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        Assert.True(await Flow.Completion.Task.WaitAsync(deadline.Token), "The genuine flow failed independent validation.");
        Assert.True(!Flow.Exchange || Flow.Validated, "The exchanged tokens failed independent validation.");
    }

    public async Task<HttpResponseMessage> SubmitAsync(IssuerEnvironment environment, HttpClient browser, string? username = null, string? password = null, string? antiforgery = null)
    {
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Interaction"] = Interaction,
            ["__RequestVerificationToken"] = antiforgery ?? Antiforgery,
            ["Username"] = username ?? environment.Username,
            ["Password"] = password ?? environment.Password
        });
        return await browser.PostAsync(new Uri(environment.Issuer, "/account/login"), content, TestContext.Current.CancellationToken);
    }
}
