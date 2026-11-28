using IdentityPlatform.Core.Features.Applications;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.ClientAssertions;

/// <summary>Retains the client selected from the exact bounded server-to-server signed machine request before native extraction.</summary>
internal sealed class ManagedAssertionTransport(string clientId)
{
    private const int FieldCount = 5;
    private static readonly string[] Fields = [OpenIddictConstants.Parameters.ClientId, OpenIddictConstants.Parameters.GrantType,
        OpenIddictConstants.Parameters.Scope, OpenIddictConstants.Parameters.ClientAssertion, OpenIddictConstants.Parameters.ClientAssertionType];

    /// <summary>Checks original field spelling, multiplicity and assertion size before the library parses the compact proof.</summary>
    internal static bool Valid(IFormCollection form) => form.Files.Count == 0 && form.Count == FieldCount &&
        form.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Fields) && form.All(field => field.Value.Count == 1) &&
        form[OpenIddictConstants.Parameters.GrantType] == OpenIddictConstants.GrantTypes.ClientCredentials &&
        form[OpenIddictConstants.Parameters.ClientAssertionType] == OpenIddictConstants.ClientAssertionTypes.JwtBearer &&
        form[OpenIddictConstants.Parameters.ClientAssertion][0] is { Length: > 0 and <= ClientAssertionShape.MaximumBytes } proof && proof.All(char.IsAscii) &&
        MachineClientRules.ReadScopes(form[OpenIddictConstants.Parameters.Scope]) is not null;

    /// <summary>Checks that extraction selected the original form client before selecting its owned native managers.</summary>
    internal static bool Matches(OpenIddictServerTransaction transaction, string? client) =>
        transaction.GetProperty<ManagedAssertionTransport>(typeof(ManagedAssertionTransport).FullName!) is { } receipt && receipt.Client == client;

    private string Client => clientId;
}
