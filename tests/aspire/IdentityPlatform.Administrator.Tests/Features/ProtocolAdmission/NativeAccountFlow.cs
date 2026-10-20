using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Supplies fixed native protocol messages and independently validates returned protected artifacts without printing them.</summary>
internal static class NativeAccountFlow
{
    private const string Challenge = "E9Melhoa2OwvFrEMTJguCHaoeK1t8URWbuGJSstw-cM";
    private const string Verifier = "dBjftJeZ4CVP-mB92K27uhbUJU1p1r_wW1gFWFOEjXk";
    private const string State = "native-account-state";
    internal const string Nonce = "native-account-nonce";
    private const string Scopes = "openid identity.admin";
    private const int ParFields = 2;

    /// <summary>Obtains a real native protected PAR reference using fresh private-key authentication and the fixed code/S256 profile.</summary>
    internal static async Task<string> PushAsync(AdministratorProtocolScenario scenario, IServiceProvider? services = null)
        => ReadReference(await NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.ParPath, PushFields(scenario), services: services));

    /// <summary>Builds the fixed valid PAR message, optionally retaining an exact assertion for cross-endpoint replay controls.</summary>
    internal static Dictionary<string, string> PushFields(AdministratorProtocolScenario scenario, string? assertion = null)
    {
        var fields = Assertion(scenario, assertion);
        fields.Add(OpenIddictConstants.Parameters.ResponseType, OpenIddictConstants.ResponseTypes.Code);
        fields.Add(OpenIddictConstants.Parameters.RedirectUri, scenario.Material.Policy.RedirectUri);
        fields.Add(OpenIddictConstants.Parameters.Scope, Scopes);
        fields.Add(OpenIddictConstants.Parameters.CodeChallenge, Challenge);
        fields.Add(OpenIddictConstants.Parameters.CodeChallengeMethod, OpenIddictConstants.CodeChallengeMethods.Sha256);
        fields.Add(OpenIddictConstants.Parameters.Prompt, OpenIddictConstants.PromptValues.None);
        fields.Add(OpenIddictConstants.Parameters.State, State);
        fields.Add(OpenIddictConstants.Parameters.Nonce, Nonce);
        return fields;
    }

    /// <summary>Accepts only the native two-field successful PAR response without printing the opaque reference.</summary>
    internal static string ReadReference(NativeAccountResult response)
    {
        RequireSuccess(response, StatusCodes.Status201Created);
        using var json = JsonDocument.Parse(response.Body);
        Assert.Equal(ParFields, json.RootElement.EnumerateObject().Count());
        var reference = json.RootElement.GetProperty(OpenIddictConstants.Parameters.RequestUri).GetString();
        Assert.True(reference?.StartsWith(OpenIddictConstants.RequestUris.Prefixes.Generic, StringComparison.Ordinal) == true);
        return reference!;
    }

    /// <summary>Submits only the native PAR reference and fixed client while carrying the genuine administrator cookie.</summary>
    internal static Task<NativeAccountResult> AuthorizeAsync(AdministratorProtocolScenario scenario, string reference, IServiceProvider? services = null) =>
        NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.AuthorizePath, new()
        {
            [OpenIddictConstants.Parameters.ClientId] = AdministratorProtocolIdentity.ClientId,
            [OpenIddictConstants.Parameters.RequestUri] = reference
        }, cookie: true, services: services);

    /// <summary>Accepts a code only from the exact fixed native query callback, preserving original state and issuer binding.</summary>
    internal static string ReadCode(AdministratorProtocolScenario scenario, NativeAccountResult response)
    {
        RequireSuccess(response, StatusCodes.Status302Found);
        Assert.True(Uri.TryCreate(response.Location, UriKind.Absolute, out var redirect));
        Assert.True(redirect.GetLeftPart(UriPartial.Path) == scenario.Material.Policy.RedirectUri);
        var query = QueryHelpers.ParseQuery(redirect.Query);
        Assert.True(query.TryGetValue(OpenIddictConstants.Parameters.State, out var state) && state.Count == 1 && state[0] == State);
        Assert.True(query.TryGetValue(OpenIddictConstants.Parameters.Iss, out var issuer) && issuer.Count == 1 && issuer[0] == scenario.Issuer.AbsoluteUri);
        Assert.False(query.ContainsKey(OpenIddictConstants.Parameters.Error));
        Assert.True(query.TryGetValue(OpenIddictConstants.Parameters.Code, out var code) && code.Count == 1 && !string.IsNullOrEmpty(code[0]));
        return code[0]!;
    }

    /// <summary>Uses a different signed client assertion for the one-time code exchange; there is no retry or password/factor renewal.</summary>
    internal static Task<NativeAccountResult> ExchangeAsync(AdministratorProtocolScenario scenario, string code, IServiceProvider? services = null)
        => NativeAccountHttp.InvokeAsync(scenario, AdministratorProtocolScenario.TokenPath, ExchangeFields(scenario, code), services: services);

    /// <summary>Builds an otherwise valid exchange while allowing a deliberately reused signed assertion in replay controls.</summary>
    internal static Dictionary<string, string> ExchangeFields(AdministratorProtocolScenario scenario, string code, string? assertion = null)
    {
        var fields = Assertion(scenario, assertion);
        fields.Add(OpenIddictConstants.Parameters.GrantType, OpenIddictConstants.GrantTypes.AuthorizationCode);
        fields.Add(OpenIddictConstants.Parameters.Code, code);
        fields.Add(OpenIddictConstants.Parameters.CodeVerifier, Verifier);
        fields.Add(OpenIddictConstants.Parameters.RedirectUri, scenario.Material.Policy.RedirectUri);
        return fields;
    }

    /// <summary>Validates signature/decryption and native persisted status in a separate owned scope rather than decoding untrusted JWT text.</summary>
    internal static Task<ClaimsPrincipal> ValidateAsync(AdministratorProtocolScenario scenario, string value, string type) =>
        new ProtocolStorageRequest(scenario.Account.Journey.Clock, TestContext.Current.CancellationToken)
            .RunAsync(scenario.Services.GetRequiredService<IServiceScopeFactory>(), async (scope, token) =>
            {
                scope.Services.GetService<ProtocolStorageSelection>()?.Select(ProtocolStorageMode.Owned);
                var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
                transaction.BaseUri = scenario.Issuer;
                transaction.EndpointType = OpenIddictServerEndpointType.Token;
                transaction.Request = new()
                {
                    ClientId = AdministratorProtocolIdentity.ClientId
                };
                var context = new OpenIddictServerEvents.ValidateTokenContext(transaction) { Token = value, CancellationToken = token };
                context.ValidTokenTypes.Add(type);
                await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
                Assert.False(context.IsRejected);
                Assert.False(context.IsRequestHandled);
                Assert.False(context.IsRequestSkipped);
                Assert.NotNull(context.TokenValidationResult);
                Assert.True(context.TokenValidationResult.IsValid);
                Assert.NotNull(context.Principal);
                return context.Principal;
            });

    /// <summary>Reports only finite control flow and exception type/stack, never serialized request or response secrets.</summary>
    internal static void RequireSuccess(NativeAccountResult response, int status)
    {
        Assert.True(response.Failure is null, response.Failure?.GetType().FullName + Environment.NewLine + response.Failure?.StackTrace);
        Assert.True(response.Claimed, response.Outcome.ToString());
        Assert.Equal(status, response.Status);
    }

    /// <summary>Signs against the exact durable fixture issuer rather than assuming the assertion helper's default audience.</summary>
    private static Dictionary<string, string> Assertion(AdministratorProtocolScenario scenario, string? assertion = null) => new()
    {
        [OpenIddictConstants.Parameters.ClientId] = AdministratorProtocolIdentity.ClientId,
        [OpenIddictConstants.Parameters.ClientAssertionType] = OpenIddictConstants.ClientAssertionTypes.JwtBearer,
        [OpenIddictConstants.Parameters.ClientAssertion] = assertion ?? scenario.Material.Sign(scenario.Account.Journey.Clock.Now.ToUnixTimeSeconds(), audience: scenario.Issuer.AbsoluteUri)
    };
}
