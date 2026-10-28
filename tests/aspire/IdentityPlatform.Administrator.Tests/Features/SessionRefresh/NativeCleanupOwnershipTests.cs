using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Exercises the selector's application and authorization binding checks with an explicit persisted corruption seam.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeCleanupOwnershipTests(BootstrapFixture fixture)
{
    private const string ForeignClient = "foreign-cleanup-characterization-client";
    private const int ForeignKeyBits = 3072;
    private const string ForeignKeyId = "foreign-cleanup-signing";

    /// <summary>A selector that points at another application's family cannot turn valid client authentication into foreign-family denial.</summary>
    [Theory, MemberData(nameof(Providers))]
    public async Task Given_ForeignApplicationOrFamilySubstitution_When_ClientAuthenticates_Then_SelectionDeniesAndBothFamiliesSurvive(string provider)
    {
        await using var issuer = await NativeCleanupFixture.CreateAsync(fixture, provider);
        var code = await NativeCleanupFlow.CodeAsync(issuer);
        var original = await NativeCleanupFlow.StateAsync(issuer, code.Intent);
        Assert.NotNull(original);
        using var key = RSA.Create(ForeignKeyBits);
        var foreign = await ForeignAsync(issuer, key);
        await DenyForeignClientAsync(issuer, key, code.Intent);
        await RequireValidAsync(issuer, original.Authorization, foreign.Authorization);
        await using var client = new NativeCleanupClient(issuer);
        foreach (var application in new[] { true, false })
        {
            await SubstituteAsync(issuer, code.Intent, original.Authorization, foreign, application);
            var sends = client.Bridge.Sends;
            var proofs = client.Bridge.ProofDigests.Count;
            var failure = await Assert.ThrowsAsync<OpenIddictExceptions.ProtocolException>(() => client.RevokeAsync(code.Intent, NativeCleanupProfile.Random()));
            Assert.Equal(sends + 1, client.Bridge.Sends);
            Assert.Equal(proofs + 1, client.Bridge.ProofDigests.Count);
            Assert.Equal(OpenIddictConstants.Errors.ServerError, failure.Error);
            Assert.Null(client.Bridge.LastFailure);
            Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, client.Bridge.LastNativeError);
            await RequireValidAsync(issuer, original.Authorization, foreign.Authorization);
        }
        await RestoreAsync(issuer, code.Intent, original.Authorization);
        var nonce = NativeCleanupProfile.Random();
        Assert.True(NativeCleanupFlow.Confirmed(await client.RevokeAsync(code.Intent, nonce), nonce));
        await RequireValidAsync(issuer, foreign.Authorization);
    }

    /// <summary>Creates an explicitly synthetic foreign native family solely for a hostile persisted-binding negative control.</summary>
    private static Task<(string Application, string Authorization)> ForeignAsync(NativeCleanupFixture fixture, RSA key) => fixture.CommitAsync(async (scope, token) =>
    {
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var descriptor = new OpenIddictApplicationDescriptor
        {
            ClientId = ForeignClient,
            ClientType = OpenIddictConstants.ClientTypes.Confidential,
            JsonWebKeySet = new JsonWebKeySet(),
            Permissions = { OpenIddictConstants.Permissions.Endpoints.Revocation }
        };
        descriptor.JsonWebKeySet.Keys.Add(JsonWebKeyConverter.ConvertFromRSASecurityKey(new RsaSecurityKey(key.ExportParameters(false)) { KeyId = ForeignKeyId }));
        var application = await applications.CreateAsync(descriptor, token);
        var id = (await applications.GetIdAsync(application, token))!;
        var authorizations = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        var family = await authorizations.CreateAsync(new OpenIddictAuthorizationDescriptor
        {
            ApplicationId = id,
            Status = OpenIddictConstants.Statuses.Valid,
            Type = OpenIddictConstants.AuthorizationTypes.AdHoc,
            Subject = NativeCleanupProfile.Subject
        }, token);
        return (id, (await authorizations.GetIdAsync(family, token))!);
    });

    /// <summary>Uses a distinct persisted client and RSA key through actual native JWT verification before selector ownership denial.</summary>
    private static async Task DenyForeignClientAsync(NativeCleanupFixture issuer, RSA key, string intent)
    {
        var proof = NativeCleanupFlow.Proof(issuer, new RsaSecurityKey(key) { KeyId = ForeignKeyId }, client: ForeignClient);
        proof[OpenIddictConstants.Parameters.Token] = intent;
        proof[OpenIddictConstants.Parameters.TokenTypeHint] = NativeCleanupProfile.SelectorType;
        proof[NativeCleanupProfile.Nonce] = NativeCleanupProfile.Random();
        var response = await NativeCleanupHttp.InvokeAsync(issuer, NativeCleanupProfile.Revoke, proof);
        Assert.Equal(StatusCodes.Status400BadRequest, response.Status);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal(OpenIddictConstants.Errors.InvalidRequest, json.RootElement.GetProperty(OpenIddictConstants.Parameters.Error).GetString());
        Assert.False(json.RootElement.TryGetProperty(NativeCleanupProfile.Confirmation, out _));
    }

    /// <summary>Changes exactly one persisted ownership field at a time while retaining a natively authenticated client request.</summary>
    private static Task<bool> SubstituteAsync(NativeCleanupFixture fixture, string intent, string original,
        (string Application, string Authorization) foreign, bool application) => fixture.CommitAsync(async (scope, token) =>
    {
        var applications = scope.Services.GetRequiredService<IOpenIddictApplicationManager>();
        var owner = await applications.FindByClientIdAsync(NativeCleanupProfile.Client, token);
        Assert.NotNull(owner);
        var tokens = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await tokens.FindByReferenceIdAsync(NativeCleanupProfile.Reference(NativeCleanupProfile.Client, intent), token);
        Assert.NotNull(row);
        var descriptor = new OpenIddictTokenDescriptor();
        await tokens.PopulateAsync(descriptor, row, token);
        descriptor.ApplicationId = application ? foreign.Application : await applications.GetIdAsync(owner, token);
        descriptor.AuthorizationId = application ? original : foreign.Authorization;
        await tokens.UpdateAsync(row, descriptor, token);
        return true;
    });

    /// <summary>Restores the original exact binding through the same native manager mutation seam.</summary>
    private static Task<bool> RestoreAsync(NativeCleanupFixture fixture, string intent, string original) =>
        SubstituteAsync(fixture, intent, original, (string.Empty, original), false);

    /// <summary>Independently reloads each affected authorization after the rejected request's scope has been disposed.</summary>
    private static Task<bool> RequireValidAsync(NativeCleanupFixture fixture, params string[] ids) => fixture.RunAsync(async (scope, token) =>
    {
        var manager = scope.Services.GetRequiredService<IOpenIddictAuthorizationManager>();
        foreach (var id in ids)
        {
            var row = await manager.FindByIdAsync(id, token);
            Assert.NotNull(row);
            Assert.Equal(OpenIddictConstants.Statuses.Valid, await manager.GetStatusAsync(row, token));
        }
        return true;
    });

    /// <summary>Requires identical foreign-binding denial on both migrated providers.</summary>
    public static IEnumerable<TheoryDataRow<string>> Providers => NativeRefreshProviders.Rows;
}
