using System.Net.Http.Headers;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Administrator.Tests.Features.Browser;
using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Reads a real callback's protected credential only inside tests, without placing it in browser storage, logs or assertion output.</summary>
internal static class ApplicationAccessFixture
{
    private const string SignedIn = "You’re signed in";
    private const string ApiPath = "/api/v1/applications";
    private const string Bearer = "Bearer";
    private const string NoStore = "no-store";
    private const string JsonProblem = "application/problem+json";

    private const int MaximumBytes = 524288;
    private const string Items = "items";
    private const string ClientId = "clientId";
    private const string RegisteredClient = "identity-admin-bff";
    private const string Status = "status";
    private static readonly string[] ProblemFields = ["type", "title", "status", "correlationId", "traceId"];

    /// <summary>Performs the native password/MFA flow and decrypts only the sole live session through its production codec.</summary>
    internal static async Task<BrowserAccessCredential> SignInAsync(BrowserHttpsFixture environment, IPage page)
    {
        await BrowserHttpsJourney.BeginAsync(environment, page);
        await BrowserHttpsJourney.PasswordAsync(environment, page);
        await BrowserHttpsJourney.FactorAsync(environment, page);
        await page.GetByRole(AriaRole.Heading, new()
        {
            Name = SignedIn,
            Exact = true
        }).WaitForAsync();
        await using var scope = environment.Client.Services.CreateAsyncScope();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        var row = await database.Set<AdminSessionTicket>().AsNoTracking().SingleAsync(item => item.RevokedAt == null, TestContext.Current.CancellationToken);
        var ticket = scope.ServiceProvider.GetRequiredService<SessionTicketCodec>().Unprotect(row);
        Assert.NotNull(ticket);
        var access = BrowserAccessCredential.Read(ticket.Properties, environment.Issuer.Account.Journey.Clock.Now);
        Assert.NotNull(access);
        return access;
    }

    /// <summary>Calls the actual API with a server-side bearer and checks safe nonredirecting status without logging the credential.</summary>
    internal static async Task RequireAsync(Uri origin, string? access, int expected)
    {
        using var client = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false });
        using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(origin, ApiPath));
        if (access is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access);
        }
        using var response = await client.SendAsync(request, TestContext.Current.CancellationToken);
        Assert.Equal(expected, (int)response.StatusCode);
        Assert.Null(response.Headers.Location);
        Assert.Equal(NoStore, response.Headers.CacheControl?.ToString());
        await RequireBodyAsync(response, access, expected);
    }
    /// <summary>Rejects extra response members or credential echoes without including private body text in assertion diagnostics.</summary>
    private static async Task RequireBodyAsync(HttpResponseMessage response, string? access, int expected)
    {
        var bytes = await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken);
        Assert.True(bytes.Length <= MaximumBytes);
        using var document = JsonDocument.Parse(bytes);
        if (response.IsSuccessStatusCode)
        {
            Assert.Contains(document.RootElement.GetProperty(Items).EnumerateArray(), item => item.GetProperty(ClientId).GetString() == RegisteredClient);
        }
        else
        {
            Assert.Equal(JsonProblem, response.Content.Headers.ContentType?.MediaType);
            Assert.Equal(expected, document.RootElement.GetProperty(Status).GetInt32());
            Assert.All(document.RootElement.EnumerateObject(), field => Assert.Contains(field.Name, ProblemFields));
            Assert.False(document.RootElement.TryGetProperty(Items, out _));
            Assert.True(access is null || !document.RootElement.GetRawText().Contains(access, StringComparison.Ordinal));
        }
    }

}
