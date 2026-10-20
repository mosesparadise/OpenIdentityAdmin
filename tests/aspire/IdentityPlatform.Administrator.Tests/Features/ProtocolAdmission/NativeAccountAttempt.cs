using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Retains in-memory inputs for one actual account issuance after its prerequisite native flow succeeds.</summary>
internal sealed class NativeAccountAttempt(AdministratorProtocolScenario scenario, bool authorization, string reference,
    string? code, string consumed, (long Tokens, long Authorizations) before)
{
    internal string Consumed => consumed;
    internal (long Tokens, long Authorizations) Before => before;

    /// <summary>Completes real PAR and optional authorization before arming an issuance-only failure.</summary>
    internal static async Task<NativeAccountAttempt> PrepareAsync(AdministratorProtocolScenario scenario, bool authorization)
    {
        var reference = await NativeAccountFlow.PushAsync(scenario);
        var consumed = await NativeAccountPersistence.ReferenceIdAsync(scenario, reference);
        string? code = null;
        if (!authorization)
        {
            code = NativeAccountFlow.ReadCode(scenario, await NativeAccountFlow.AuthorizeAsync(scenario, reference));
            var original = await NativeAccountFlow.ValidateAsync(scenario, code, OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode);
            consumed = original.GetTokenId()!;
        }
        return new(scenario, authorization, reference, code, consumed, await NativeAccountPersistence.CountAsync(scenario));
    }

    /// <summary>Performs the selected native operation once, without retrying consumed PAR references or authorization codes.</summary>
    internal Task<NativeAccountResult> InvokeAsync() => authorization ? NativeAccountFlow.AuthorizeAsync(scenario, reference) :
        NativeAccountFlow.ExchangeAsync(scenario, code!);

    /// <summary>Independently checks output suppression, complete issuance rollback and durable earlier native consumption after cleanup.</summary>
    internal async Task RequireDeniedAsync(NativeAccountResult response)
    {
        Assert.False(response.Claimed);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, response.Status);
        Assert.True(response.Body.Length == 0 && response.Location.Length == 0);
        Assert.Equal(before, await NativeAccountPersistence.CountAsync(scenario));
        Assert.True(await NativeAccountPersistence.IsRedeemedAsync(scenario, consumed));
    }
}
