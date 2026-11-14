using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Features.Applications;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Builds real consent policy through protected HTTPS management and observes current identity state independently.</summary>
internal static class ManagedConsentTest
{
    private const string OrdersDisplay = "Read orders";
    private const string ApiKind = "api";
    private const string OrdersDescription = "Read your orders";
    private const string IdentifierField = "id";
    private const string NoDisclosure = "none";
    private const string AccessField = "access_token";
    private const string Bearer = "Bearer";
    private const string CookieHeader = "Set-Cookie";
    private const string SubjectField = "sub";
    private const string EmailField = "email";
    private const string VerifiedField = "email_verified";
    private const string InvalidChallenge = "error=\"invalid_token\", error_description=\"The request could not be completed.\"";

    internal const string Address = "consented@example.test";
    internal const string ChangedAddress = "current@example.test";
    internal const string UserInfo = "/connect/userinfo";
    internal const string Resource = "orders-api";
    internal const string ApiScope = "orders.read";
    private const string Scopes = "/api/v1/scopes";
    private const string Audiences = "/api/v1/audiences";
    private const string Audience = "{\"resource\":\"orders-api\",\"displayName\":\"Orders API\",\"owner\":\"Orders team\",\"enabled\":true}";
    private const string Assign = "{\"scopes\":[\"email\",\"orders.read\"]}";

    /// <summary>Assigns email and one API scope using genuine permission, fresh MFA and optimistic concurrency checks.</summary>
    internal static async Task SeedAsync(ManagedCacheTestContext test)
    {
        using var email = await test.Http.CatalogueAsync(HttpMethod.Post, Scopes, ApplicationScopeHttpTests.Scope);
        Assert.Equal(HttpStatusCode.Created, email.StatusCode);
        using var audience = await test.Http.CatalogueAsync(HttpMethod.Post, Audiences, Audience);
        Assert.Equal(HttpStatusCode.Created, audience.StatusCode);
        using var body = JsonDocument.Parse(await audience.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var scope = JsonSerializer.Serialize(new
        {
            name = ApiScope,
            displayName = OrdersDisplay,
            kind = ApiKind,
            enabled = true,
            consentText = OrdersDescription,
            audienceId = body.RootElement.GetProperty(IdentifierField).GetString(),
            disclosure = NoDisclosure
        });
        using var api = await test.Http.CatalogueAsync(HttpMethod.Post, Scopes, scope);
        Assert.Equal(HttpStatusCode.Created, api.StatusCode);
        using var assigned = await test.Http.CatalogueAsync(HttpMethod.Put, ApplicationScopeHttpTests.Path(test.Id), Assign,
            ApplicationVersion.Create(test.Id, 1), direct: false, csrf: await test.Http.TokenAsync());
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
    }

    /// <summary>Changes synthetic Identity email independently of consent storage, preserving the same account generation for current-data projection checks.</summary>
    internal static async Task EmailAsync(ManagedCacheTestContext test, string? email, bool verified)
    {
        await using var scope = test.Http.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var username = test.Http.Environment.Issuer.Account.Journey.Lease.Username;
        var user = await database.Users.SingleAsync(row => row.UserName == username, TestContext.Current.CancellationToken);
        user.Email = email;
        user.EmailConfirmed = verified;
        await database.SaveChangesAsync(TestContext.Current.CancellationToken);
    }

    /// <summary>Uses the real HTTPS UserInfo endpoint with exactly one explicit bearer transport.</summary>
    internal static async Task<HttpResponseMessage> ReadAsync(ManagedCacheTestContext test, string access, bool post = false, bool form = false)
    {
        using var request = new HttpRequestMessage(post ? HttpMethod.Post : HttpMethod.Get, UserInfo);
        if (post)
        {
            request.Content = new FormUrlEncodedContent(form ? new Dictionary<string, string> { [AccessField] = access } : []);
        }
        if (!form)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(Bearer, access);
        }
        return await test.Http.IssuerClient.SendAsync(request, TestContext.Current.CancellationToken);
    }

    /// <summary>Checks the complete finite projection, boolean JSON type, no-cache headers and absence of cookies or token fields.</summary>
    internal static async Task RequireAsync(HttpResponseMessage response, string subject, string? email, bool verified)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.False(response.Headers.Contains(CookieHeader));
        Assert.Empty(response.Headers.WwwAuthenticate);
        using var body = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        Assert.True(body.RootElement.GetProperty(SubjectField).GetString() == subject);
        Assert.True(body.RootElement.EnumerateObject().Select(field => field.Name).ToHashSet(StringComparer.Ordinal)
            .SetEquals(email is null ? [SubjectField] : [SubjectField, EmailField, VerifiedField]));
        if (email is not null)
        {
            Assert.True(body.RootElement.GetProperty(EmailField).GetString() == email);
            Assert.Equal(verified ? JsonValueKind.True : JsonValueKind.False, body.RootElement.GetProperty(VerifiedField).ValueKind);
        }
    }

    /// <summary>Requires the native safe empty bearer denial with no personal data, cookie, redirect or credential body.</summary>
    internal static async Task DeniedAsync(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.False(response.Headers.Contains(CookieHeader));
        Assert.Null(response.Headers.Location);
        var challenge = Assert.Single(response.Headers.WwwAuthenticate);
        Assert.Equal(Bearer, challenge.Scheme);
        Assert.Equal(InvalidChallenge, challenge.Parameter);
        Assert.Empty(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
    }
}
