using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Proves native authentication and endpoint permission still precede the private selector adapter.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeCleanupAuthenticationTests(BootstrapFixture fixture)
{
    private const int WrongKeyBits = 3072;
    private const int ExpiredProofAgeSeconds = 3600;
    private const string ForeignAudience = "https://foreign-issuer.example.test/";
    private const string Malformed = "malformed";

    /// <summary>Missing proof, wrong signature and wrong audience are denied without changing any family state; valid native signing restores success.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_InvalidClientProof_When_SelectorIsKnown_Then_NativeAuthenticationDeniesBeforeFamilyMutation(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        using var wrong = RSA.Create(WrongKeyBits);
        var missing = NativeCleanupFlow.Proof(issuer);
        missing.Remove(OpenIddictConstants.Parameters.ClientAssertion);
        missing.Remove(OpenIddictConstants.Parameters.ClientAssertionType);
        foreach (var (proof, expected) in new[]
        {
            (missing, OpenIddictConstants.Errors.InvalidClient),
            (NativeCleanupFlow.Proof(issuer, new RsaSecurityKey(wrong)), OpenIddictConstants.Errors.InvalidClient),
            (NativeCleanupFlow.Proof(issuer, audience: ForeignAudience), OpenIddictConstants.Errors.InvalidToken),
            (NativeCleanupFlow.Proof(issuer, ageSeconds: ExpiredProofAgeSeconds), OpenIddictConstants.Errors.InvalidClient)
        })
        {
            var response = await RevokeAsync(issuer, code.Intent, NativeCleanupProfile.Random(), proof);
            RequireProofDenied(response, expected);
            Assert.Equal(OpenIddictConstants.Statuses.Valid, (await NativeCleanupFlow.StateAsync(issuer, code.Intent))!.Status);
        }
        await using var client = new NativeCleanupClient(issuer);
        var nonce = NativeCleanupProfile.Random();
        Assert.True(NativeCleanupFlow.Confirmed(await client.RevokeAsync(code.Intent, nonce), nonce));
    }

    /// <summary>Unknown or malformed targets and nonces cannot cause denial of an unrelated valid family.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_InvalidSelectorOrNonce_When_ClientAuthenticates_Then_NoFamilyMutationOrConfirmationOccurs(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        foreach (var (intent, nonce) in new[]
        {
            (NativeCleanupProfile.Random(), NativeCleanupProfile.Random()),
            (Malformed, NativeCleanupProfile.Random()),
            (code.Intent, Malformed)
        })
        {
            RequireError(await RevokeAsync(issuer, intent, nonce, NativeCleanupFlow.Proof(issuer)), OpenIddictConstants.Errors.InvalidRequest);
            Assert.Equal(OpenIddictConstants.Statuses.Valid, (await NativeCleanupFlow.StateAsync(issuer, code.Intent))!.Status);
        }
        await using var client = new NativeCleanupClient(issuer);
        var validNonce = NativeCleanupProfile.Random();
        Assert.True(NativeCleanupFlow.Confirmed(await client.RevokeAsync(code.Intent, validNonce), validNonce));
    }

    /// <summary>Revocation needs its own native permission; removing token issuance permission alone still permits authenticated cleanup.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_NativePermissionChanges_When_CleanupIsRequested_Then_RevocationPermissionIsIndependentOfIssuance(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        await PermissionAsync(issuer, OpenIddictConstants.Permissions.Endpoints.Revocation, false);
        var denied = await RevokeAsync(issuer, code.Intent, NativeCleanupProfile.Random(), NativeCleanupFlow.Proof(issuer));
        RequireError(denied, OpenIddictConstants.Errors.UnauthorizedClient);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, (await NativeCleanupFlow.StateAsync(issuer, code.Intent))!.Status);
        await PermissionAsync(issuer, OpenIddictConstants.Permissions.Endpoints.Revocation, true);
        await PermissionAsync(issuer, OpenIddictConstants.Permissions.Endpoints.Token, false);
        RequireError(await NativeCleanupFlow.ExchangeAsync(issuer, code), OpenIddictConstants.Errors.UnauthorizedClient);
        await using var client = new NativeCleanupClient(issuer);
        var nonce = NativeCleanupProfile.Random();
        Assert.True(NativeCleanupFlow.Confirmed(await client.RevokeAsync(code.Intent, nonce), nonce));
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, (await NativeCleanupFlow.StateAsync(issuer, code.Intent))!.Status);
    }

    /// <summary>Records the remaining durable replay requirement: native crypto accepts the exact same still-current proof twice.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_ExactClientAssertionReplay_When_OnlyNativeCryptoIsConfigured_Then_NoDurableReplayProtectionIsImplied(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        var proof = NativeCleanupFlow.Proof(issuer);
        var nonce = NativeCleanupProfile.Random();
        var first = await RevokeAsync(issuer, code.Intent, nonce, proof);
        var second = await RevokeAsync(issuer, code.Intent, nonce, proof);
        Assert.Equal(200, first.Status);
        Assert.Equal(200, second.Status);
        using var firstJson = JsonDocument.Parse(first.Body);
        using var secondJson = JsonDocument.Parse(second.Body);
        Assert.True(firstJson.RootElement.GetProperty(NativeCleanupProfile.Confirmation).GetBoolean());
        Assert.True(secondJson.RootElement.GetProperty(NativeCleanupProfile.Confirmation).GetBoolean());
        Assert.Equal(OpenIddictConstants.Statuses.Revoked, (await NativeCleanupFlow.StateAsync(issuer, code.Intent))!.Status);
    }

    /// <summary>Adds only the selector extension fields to an independently signed native client request.</summary>
    private static Task<NativeCleanupResponse> RevokeAsync(NativeCleanupFixture issuer, string intent, string nonce, Dictionary<string, string> fields)
    {
        fields[OpenIddictConstants.Parameters.Token] = intent;
        fields[OpenIddictConstants.Parameters.TokenTypeHint] = NativeCleanupProfile.SelectorType;
        fields[NativeCleanupProfile.Nonce] = nonce;
        return NativeCleanupHttp.InvokeAsync(issuer, NativeCleanupProfile.Revoke, fields);
    }

    /// <summary>Asserts protocol error shape without including a synthetic proof or response payload in diagnostics.</summary>
    private static void RequireError(NativeCleanupResponse response, string expected)
    {
        Assert.InRange(response.Status, StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal(expected, json.RootElement.GetProperty(OpenIddictConstants.Parameters.Error).GetString());
        Assert.False(json.RootElement.TryGetProperty(NativeCleanupProfile.Confirmation, out _));
    }

    /// <summary>Distinguishes native proof rejection from RFC7009 normalization; an empty HTTP200 is never cleanup confirmation.</summary>
    private static void RequireProofDenied(NativeCleanupResponse response, string expected)
    {
        Assert.Equal(expected, response.NativeError);
        if (expected == OpenIddictConstants.Errors.InvalidToken)
        {
            Assert.Equal(StatusCodes.Status200OK, response.Status);
            using var json = JsonDocument.Parse(response.Body);
            Assert.Empty(json.RootElement.EnumerateObject());
            return;
        }
        RequireError(response, expected);
    }

    /// <summary>Changes native persisted application permissions in a fresh committed owner, without pretending to disable a B3 profile.</summary>
    private static Task<bool> PermissionAsync(NativeCleanupFixture issuer, string permission, bool enabled) => issuer.CommitAsync(async (scope, token) =>
    {
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var application = await applications.FindByClientIdAsync(NativeCleanupProfile.Client, token);
        Assert.NotNull(application);
        var descriptor = new OpenIddictApplicationDescriptor();
        await applications.PopulateAsync(descriptor, application, token);
        if (enabled)
        {
            descriptor.Permissions.Add(permission);
        }
        else
        {
            descriptor.Permissions.Remove(permission);
        }
        await applications.UpdateAsync(application, descriptor, token);
        return true;
    });

    /// <summary>Requires identical native acceptance and denial cases for PostgreSQL and SQL Server.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => NativeRefreshProviders.Rows;
}
