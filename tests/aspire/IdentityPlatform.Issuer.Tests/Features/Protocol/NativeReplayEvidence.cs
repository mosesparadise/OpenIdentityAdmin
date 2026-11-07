using System.Globalization;
using System.Text;
using System.Text.Json;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Issuer.Tests.Features.Protocol;

/// <summary>Reports finite native lifecycle states only when exact replay revocation fails, without emitting token bodies, identities or credentials.</summary>
internal static class NativeReplayEvidence
{
    private const string ErrorUri = "error_uri";
    private const string RedeemedReplay = "ID2010";
    private const string InvalidCode = "ID2016";
    private const string Other = "other";
    private const string Missing = "missing";
    private const string Delimiter = ",";
    private const int MaximumEntries = 8;
    private const string Code = "code";
    private const string Access = "access";
    private const string Identity = "identity";
    private const string StatusSeparator = ":";
    private static readonly CompositeFormat Failure = CompositeFormat.Parse("Native replay requires revoked access; actual={0}; loser={1}; replay={2}; family(first {3})={4}.");

    /// <summary>Keeps the exact Revoked requirement; only a failing observation triggers bounded additional native status reads.</summary>
    internal static async Task RequireRevokedAsync(IOpenIddictTokenManager manager, object access, JsonElement loser, JsonElement replay)
    {
        var token = TestContext.Current.CancellationToken;
        if (await manager.HasStatusAsync(access, OpenIddictConstants.Statuses.Revoked, token))
        {
            return;
        }
        var status = await manager.GetStatusAsync(access, token);
        var authorization = await manager.GetAuthorizationIdAsync(access, token);
        var family = new List<string>();
        if (!string.IsNullOrEmpty(authorization))
        {
            await foreach (var entry in manager.FindByAuthorizationIdAsync(authorization, token))
            {
                family.Add(Type(await manager.GetTypeAsync(entry, token)) + StatusSeparator + Status(await manager.GetStatusAsync(entry, token)));
                if (family.Count == MaximumEntries)
                {
                    break;
                }
            }
        }
        family.Sort(StringComparer.Ordinal);
        Assert.Fail(string.Format(CultureInfo.InvariantCulture, Failure, Status(status), Error(loser), Error(replay), MaximumEntries, string.Join(Delimiter, family)));
    }

    /// <summary>Restricts library or corrupt-store status values to a finite vocabulary before diagnostic output.</summary>
    private static string Status(string? value) => value switch
    {
        OpenIddictConstants.Statuses.Valid or OpenIddictConstants.Statuses.Inactive or OpenIddictConstants.Statuses.Redeemed or
            OpenIddictConstants.Statuses.Revoked or OpenIddictConstants.Statuses.Rejected => value,
        null => Missing,
        _ => Other
    };

    /// <summary>Labels only expected native credential families without exposing arbitrary stored metadata.</summary>
    private static string Type(string? value) => value switch
    {
        OpenIddictConstants.TokenTypeIdentifiers.Private.AuthorizationCode => Code,
        OpenIddictConstants.TokenTypeIdentifiers.AccessToken => Access,
        OpenIddictConstants.TokenTypeIdentifiers.IdentityToken => Identity,
        _ => Other
    };

    /// <summary>Retains only two reviewed library error identifiers; arbitrary URI components never enter output.</summary>
    private static string Error(JsonElement body)
    {
        if (!body.TryGetProperty(ErrorUri, out var value) || value.ValueKind != JsonValueKind.String ||
            !Uri.TryCreate(value.GetString(), UriKind.Absolute, out var uri))
        {
            return Missing;
        }
        var id = uri.Segments.LastOrDefault();
        return id is RedeemedReplay or InvalidCode ? id : Other;
    }
}
