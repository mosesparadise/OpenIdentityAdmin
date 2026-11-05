using System.Globalization;
using System.Net;
using System.Text.Json;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Shares independently submitted wire values and complete persisted update assertions across real API/BFF provider journeys.</summary>
internal static class ApplicationHttpUpdateEvidence
{
    internal const string Edit = "{\"displayName\":\"HTTP updated\",\"redirectUris\":[\"https://updated.example.test/callback\"]}";
    private const string Format = "yyyy-MM-dd'T'HH:mm:ss'Z'";
    private const int NativeRoundingMilliseconds = 500;
    private const int LifetimeMinutes = 30;
    private const string Target = "Application";
    private const string Actor = "User";
    private const string Accepted = "Accepted";
    private const string Edited = "ApplicationEdited";
    private const string Replaced = "ApplicationSecretReplaced";
    private const string Application = "application";
    private const string Credential = "credential";
    private static readonly string[] Fields = ["clientId", "credential", "displayName", "editable", "enabled", "expiresAt", "id", "profile", "redirectUris"];
    private static readonly string[] CredentialFields = ["expiresAt", "issuedAt"];
    private const string NoSniff = "nosniff";
    private const string NoSniffHeader = "X-Content-Type-Options";

    /// <summary>Requires an explicit future UTC expiry and literal confirmation using independently named wire fields.</summary>
    internal static string Replacement(DateTimeOffset now) => JsonSerializer.Serialize(new
    {
        confirmed = true,
        credentialExpiresAt = now.AddMinutes(LifetimeMinutes).ToString(Format, CultureInfo.InvariantCulture)
    });

    /// <summary>Captures detached SQL observations through a fresh scope after endpoint-owned cleanup.</summary>
    internal static async Task<ApplicationUpdateSnapshot> SnapshotAsync(ApplicationHttpFixture test, string id)
    {
        await using var scope = test.Scope();
        return await ApplicationUpdateSnapshot.ReadAsync(scope.ServiceProvider.GetRequiredService<PlatformDbContext>(), id, TestContext.Current.CancellationToken);
    }

    /// <summary>Hashes every persisted scalar field of a protected native registration without requiring a managed policy row.</summary>
    internal static async Task<string> NativeDigestAsync(ApplicationHttpFixture test, string id)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var native = await database.Set<OpenIddictEntityFrameworkCoreApplication>().AsNoTracking()
            .SingleAsync(row => row.Id == id, TestContext.Current.CancellationToken);
        return ApplicationUpdateSnapshot.Hash(database.Entry(native).Properties.OrderBy(value => value.Metadata.Name, StringComparer.Ordinal)
            .ToDictionary(value => value.Metadata.Name, value => value.CurrentValue));
    }

    /// <summary>Observes the actual native access-token deadline and original continuation, checking native nearest-second expires_in rounding without using the receipt to extend API authority.</summary>
    internal static async Task<DateTimeOffset> NativeDeadlineAsync(ApplicationHttpFixture test)
    {
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await database.Set<IssuerContinuation>().Where(row => row.SessionId == test.SourceSession)
            .Select(row => row.ExpiresAt).SingleAsync(TestContext.Current.CancellationToken);
        var native = await database.Set<OpenIddictEntityFrameworkCoreToken>()
            .Where(row => row.Subject == test.Environment.Issuer.Account.ActorId && row.Type == OpenIddictConstants.TokenTypeIdentifiers.AccessToken)
            .Select(row => row.ExpirationDate).SingleAsync(TestContext.Current.CancellationToken);
        var expiry = DateTimeOffset.FromUnixTimeSeconds(intent);
        Assert.Equal(expiry.UtcDateTime, native);
        Assert.InRange(test.AccessExpiresAt, expiry.AddMilliseconds(-NativeRoundingMilliseconds), expiry.AddMilliseconds(NativeRoundingMilliseconds));
        return expiry;
    }

    /// <summary>Checks exact response and native/policy/audit effects; one-time plaintext is consumed only in memory for native hash verification.</summary>
    internal static async Task<string> RequireAsync(ApplicationHttpFixture test, ApplicationUpdateSnapshot before, HttpResponseMessage response,
        string body, bool replacement, string previousSecret)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl?.NoStore == true);
        Assert.Equal(NoSniff, Assert.Single(response.Headers.GetValues(NoSniffHeader)));
        var id = before.Policy.ApplicationId;
        Assert.Equal(ApplicationVersion.Create(id, before.Policy.Revision + 1), response.Headers.ETag!.ToString());
        var previous = ApplicationVersion.Create(id, before.Policy.Revision);
        var command = replacement
            ? ApplicationMutationCommand.Replace(id, previous, JsonSerializer.Deserialize<ApplicationSecretReplacement>(body, JsonSerializerOptions.Web)!, test.Now)!
            : ApplicationMutationCommand.Edit(id, previous, JsonSerializer.Deserialize<ApplicationEdit>(body, JsonSerializerOptions.Web)!)!;
        var after = await SnapshotAsync(test, id);
        ApplicationUpdateOracle.RequireState(test.Now, before, after, command);
        RequireAudit(test, before, after, replacement);
        await RequireDetailsAsync(test, after, response, replacement);
        var secret = previousSecret;
        if (replacement)
        {
            secret = (await ApplicationHttpTests.CreatedAsync(response, HttpStatusCode.OK)).ClientSecret;
            Assert.False(await AcceptsAsync(test, id, previousSecret));
        }
        Assert.True(await AcceptsAsync(test, id, secret));
        return secret;
    }

    /// <summary>Matches every safe response field to fresh persisted values and rejects any nested private or credential-hash member.</summary>
    private static async Task RequireDetailsAsync(ApplicationHttpFixture test, ApplicationUpdateSnapshot state, HttpResponseMessage response, bool replacement)
    {
        using var json = JsonDocument.Parse(await response.Content.ReadAsByteArrayAsync(TestContext.Current.CancellationToken));
        var root = replacement ? json.RootElement.GetProperty(Application) : json.RootElement;
        Assert.Equal(Fields, root.EnumerateObject().Select(value => value.Name).Order());
        Assert.Equal(CredentialFields, root.GetProperty(Credential).EnumerateObject().Select(value => value.Name).Order());
        var details = root.Deserialize<ApplicationDetails>(JsonSerializerOptions.Web)!;
        Assert.Equal(state.Policy.ApplicationId, details.Id);
        Assert.Equal(state.ClientId, details.ClientId);
        Assert.Equal(state.Display, details.DisplayName);
        Assert.Equal(state.Redirects.Order(StringComparer.Ordinal), details.RedirectUris.Order(StringComparer.Ordinal));
        Assert.Equal(state.Policy.Profile, details.Profile);
        Assert.True(details.Editable);
        Assert.Equal(state.Policy.Enabled, details.Enabled);
        Assert.Equal(state.Policy.CredentialIssuedAt, details.Credential!.IssuedAt.ToUnixTimeSeconds());
        Assert.Equal(state.Policy.CredentialExpiresAt, details.Credential.ExpiresAt.ToUnixTimeSeconds());
        Assert.True(details.ExpiresAt > test.Now);
    }

    /// <summary>Requires exactly one correctly attributed action while retaining every field of all prior application audit rows.</summary>
    private static void RequireAudit(ApplicationHttpFixture test, ApplicationUpdateSnapshot before, ApplicationUpdateSnapshot after, bool replacement)
    {
        var prior = before.Audits.Select(value => value.Id).ToHashSet();
        Assert.Equal(ApplicationUpdateSnapshot.Hash(before.Audits), ApplicationUpdateSnapshot.Hash(after.Audits.Where(value => prior.Contains(value.Id))));
        var audit = Assert.Single(after.Audits, value => !prior.Contains(value.Id));
        Assert.Equal(replacement ? Replaced : Edited, audit.Event);
        Assert.Equal(test.Environment.Issuer.Account.ActorId, audit.ActorId);
        Assert.Equal(Actor, audit.ActorType);
        Assert.Equal(Target, audit.TargetType);
        Assert.Equal(before.Policy.ApplicationId, audit.TargetId);
        Assert.Equal(Accepted, audit.Outcome);
        Assert.NotEqual(Guid.Empty, audit.CorrelationId);
        Assert.Null(audit.RelatedCorrelationId);
        Assert.Equal(test.Now.ToUnixTimeSeconds(), audit.OccurredAt);
    }

    /// <summary>Uses the native manager's real persisted hash validator without exposing either secret in assertion diagnostics.</summary>
    private static async Task<bool> AcceptsAsync(ApplicationHttpFixture test, string id, string secret)
    {
        await using var scope = test.Scope();
        var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();
        var native = await manager.FindByIdAsync(id, TestContext.Current.CancellationToken);
        return native is not null && await manager.ValidateClientSecretAsync(native, secret, TestContext.Current.CancellationToken);
    }
}
