using System.Text.Json;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;

namespace IdentityPlatform.AuthServer.Features.Protocol.Administrator;

/// <summary>Restricts the fixed PAR response to its native reference and finite numeric lifetime.</summary>
internal static class NativeParResponse
{
    private const int ParameterCount = 2;

    /// <summary>Rejects extra credential fields and malformed or unbounded lifetimes before issuance can commit.</summary>
    internal static bool Allows(OpenIddictResponse response)
    {
        var parameters = response.GetParameters();
        if (parameters.Count != ParameterCount || !parameters.ContainsKey(OpenIddictConstants.Parameters.RequestUri) ||
            !parameters.TryGetValue(OpenIddictConstants.Parameters.ExpiresIn, out var lifetime) || string.IsNullOrEmpty(response.RequestUri))
        {
            return false;
        }
        var value = (JsonElement)lifetime;
        return value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out var seconds) &&
            seconds is > 0 and <= AdministratorAssertion.LifetimeSeconds;
    }
}
