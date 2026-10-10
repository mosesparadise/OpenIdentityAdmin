using System.Net;

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

    public static async Task<TestLogin> BeginAsync(IssuerEnvironment environment, HttpClient browser, string? prompt = null, string? maximumAge = null)
    {
        var flow = environment.RelyingParty.Prepare(prompt: prompt, maximumAge: maximumAge);
        using var response = await browser.GetAsync(environment.RelyingParty.StartUri(flow), TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("/account/login", response.RequestMessage!.RequestUri!.AbsolutePath);
        var html = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        return new TestLogin
        {
            Flow = flow,
            Interaction = IssuerEnvironment.InputValue(html, "Interaction"),
            Antiforgery = IssuerEnvironment.InputValue(html, "__RequestVerificationToken")
        };
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
