using System.Text.Json;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Checks actual protected output and durable native row deltas after both contenders finish.</summary>
internal static class NativeAuthorityRaceOutcome
{
    private const int ExchangeTokens = 2;

    /// <summary>The winning issuance keeps its original artifact; a mutation-first denial creates no code, access token or authorization.</summary>
    internal static async Task RequireAsync(AdministratorProtocolScenario scenario, NativeAccountAttempt attempt, NativeAccountResult response,
        bool authorization, bool issuanceFirst, NativeAuthorityMutation.Change change)
    {
        Assert.True(response.Failure is null, response.Failure?.GetType().FullName + Environment.NewLine + response.Failure?.StackTrace);
        Assert.False(response.HasCookie);
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, attempt.Consumed));
        var after = await NativeAccountPersistence.CountAsync(scenario);
        if (!issuanceFirst)
        {
            Assert.Equal(attempt.Before, after);
            var error = !authorization || change == NativeAuthorityMutation.Change.Registration ? OpenIddictConstants.Errors.InvalidGrant :
                change == NativeAuthorityMutation.Change.Session ? OpenIddictConstants.Errors.LoginRequired : OpenIddictConstants.Errors.AccessDenied;
            if (authorization)
            {
                NativeAccountDenialTests.RequireRedirect(scenario, response, error);
            }
            else
            {
                NativeAccountDenialTests.RequireJson(response, error);
            }
            return;
        }
        Assert.Equal(attempt.Before.Tokens + (authorization ? 1 : ExchangeTokens), after.Tokens);
        Assert.Equal(attempt.Before.Authorizations + (authorization ? 1 : 0), after.Authorizations);
        await RequireArtifactAsync(scenario, response, authorization, change);
    }

    /// <summary>Uses actual native signature/decryption and durable entry validation; post-commit source loss is not offline token revocation.</summary>
    private static async Task RequireArtifactAsync(AdministratorProtocolScenario scenario, NativeAccountResult response, bool authorization, NativeAuthorityMutation.Change change)
    {
        string value;
        if (authorization)
        {
            value = NativeAccountFlow.ReadCode(scenario, response);
        }
        else
        {
            NativeAccountFlow.RequireSuccess(response, StatusCodes.Status200OK);
            using var json = JsonDocument.Parse(response.Body);
            Assert.False(json.RootElement.TryGetProperty(OpenIddictConstants.Parameters.RefreshToken, out _));
            value = json.RootElement.GetProperty(OpenIddictConstants.Parameters.AccessToken).GetString()!;
            _ = await NativeIdentityTokenOracle.ValidateAsync(scenario, json.RootElement.GetProperty(OpenIddictConstants.Parameters.IdToken).GetString()!, value);
        }
        var principal = authorization && change == NativeAuthorityMutation.Change.Registration ? await NativeAuthorityCodeOracle.ReadAsync(scenario, value) :
            await NativeAccountFlow.ValidateAsync(scenario, value, authorization ?
            OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode : OpenIddictConstants.TokenTypeIdentifiers.AccessToken);
        Assert.True(principal.GetClaim(OpenIddictConstants.Claims.Subject) == scenario.Account.ActorId);
        Assert.True(!string.IsNullOrEmpty(principal.GetAuthorizationId()));
    }
}
