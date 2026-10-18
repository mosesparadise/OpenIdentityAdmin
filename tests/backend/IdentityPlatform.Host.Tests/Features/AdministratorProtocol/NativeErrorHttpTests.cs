using System.Net;
using System.Text.Json;
using IdentityPlatform.Host.Tests.Features.Accounts;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using Microsoft.Extensions.Configuration;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Exercises real production native error formatting for early transport rejections without any database access.</summary>
public sealed class NativeErrorHttpTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const string WindowsNewLine = "\r\n";
    private const string UnixNewLine = "\n";
    private const string EnabledSetting = "Identity:AdministratorProtocol:Enabled";
    private const string RedirectSetting = "Identity:AdministratorProtocol:RedirectUri";
    private const string PublicKeySetting = "Identity:AdministratorProtocol:PublicKey";
    private const string TokenPath = "/connect/token";
    private const string ParPath = "/connect/par";
    private const string AuthorizePath = "/connect/authorize";
    private const string InvalidClient = "invalid_client";
    private const string CookieHeader = "Set-Cookie";
    private const string ExpectedChallenge = "Basic error=\"invalid_client\", error_description=\"The request could not be completed.\"";
    private const string ExpectedLocal = "error:invalid_client\nerror_description:The request could not be completed.\n";

    private const string Callback = "https://administrator.example.test/callback";
    private const string Client = "identity-admin-bff";
    private const string ClientField = "client_id";
    private const string ErrorField = "error";
    private const string DescriptionField = "error_description";
    private const string Description = "The request could not be completed.";
    private const int ErrorFields = 2;

    /// <summary>Checks the pinned native invalid_client response includes both safe JSON and the single Basic challenge.</summary>
    [Theory]
    [InlineData(TokenPath)]
    [InlineData(ParPath)]
    public async Task Given_MissingPrivateKeyAssertion_When_NativeTransportRejects_Then_SafeNativeAuthenticationErrorPublishes(string path)
    {
        await using var host = await AccountHost.StartAsync(false, true, configureConfiguration: Configure);
        using var form = new FormUrlEncodedContent([new(ClientField, Client)]);
        using var response = await host.Client.PostAsync(path, form, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.Equal(ErrorFields, body.RootElement.EnumerateObject().Count());
        Assert.Equal(InvalidClient, body.RootElement.GetProperty(ErrorField).GetString());
        Assert.Equal(Description, body.RootElement.GetProperty(DescriptionField).GetString());
        Assert.Equal(ExpectedChallenge, Assert.Single(response.Headers.WwwAuthenticate).ToString());
        Assert.Null(response.Headers.Location);
        Assert.False(response.Headers.Contains(CookieHeader));
    }

    /// <summary>Checks invalid authorization requests stay local with no state, issuer, cookie or redirect disclosure.</summary>
    [Fact]
    public async Task Given_MissingAuthorizationIdentity_When_NativeTransportRejects_Then_ErrorStaysLocal()
    {
        await using var host = await AccountHost.StartAsync(false, true, configureConfiguration: Configure);
        using var response = await host.Client.GetAsync(AuthorizePath, TestContext.Current.CancellationToken);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        Assert.Equal(ExpectedLocal, body.Replace(WindowsNewLine, UnixNewLine, StringComparison.Ordinal));
        Assert.Null(response.Headers.Location);
        Assert.Empty(response.Headers.WwwAuthenticate);
        Assert.False(response.Headers.Contains(CookieHeader));
    }

    /// <summary>Enables the real internal administrator profile with public-only registration settings and no native rows.</summary>
    private void Configure(IConfiguration configuration)
    {
        configuration[EnabledSetting] = bool.TrueString;
        configuration[RedirectSetting] = Callback;
        configuration[PublicKeySetting] = fixture.Json;
    }
}
