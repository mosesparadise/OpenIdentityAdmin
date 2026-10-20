using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Requires genuine protected issuance and durable effects before a mutation can be credited with a security bypass.</summary>
internal static class NativeMutationGrant
{
    private const int ExchangeTokenRows = 2;

    /// <summary>Validates native access and ID tokens, original subject/authorization, redemption and exact retained-row effects.</summary>
    internal static async Task RequireAsync(AdministratorProtocolScenario scenario, NativeAccountResult response,
        ClaimsPrincipal code, (long Tokens, long Authorizations) before)
    {
        NativeAccountFlow.RequireSuccess(response, StatusCodes.Status200OK);
        Assert.False(response.HasCookie);
        Assert.True(response.Location.Length == 0);
        using var json = JsonDocument.Parse(response.Body);
        var value = json.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!;
        var access = await NativeAccountFlow.ValidateAsync(scenario, value, OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
        var identity = await NativeIdentityTokenOracle.ValidateAsync(scenario,
            json.RootElement.GetProperty(OpenIddictConstants.Parameters.IdToken).GetString()!, value);
        Assert.True(access.GetClaim(OpenIddictConstants.Claims.Subject) == code.GetClaim(OpenIddictConstants.Claims.Subject));
        Assert.True(access.GetAuthorizationId() == code.GetAuthorizationId() && !string.IsNullOrEmpty(access.GetTokenId()));
        Assert.True(identity.GetClaim(OpenIddictConstants.Claims.Subject) == code.GetClaim(OpenIddictConstants.Claims.Subject));
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, code.GetTokenId()!));
        Assert.Equal((before.Tokens + ExchangeTokenRows, before.Authorizations), await NativeAccountPersistence.CountAsync(scenario));
    }
}
