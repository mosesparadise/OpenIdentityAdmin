using System.Security.Claims;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Represents canonical B3 transport data only; parsing never proves a completed ceremony, attachment or source-session authority.</summary>
internal sealed class BrowserLoginBinding
{
    internal const string IntentParameter = "admin_login_intent";
    internal const string CompletionParameter = "admin_login_completion";
    internal const string IntentClaim = "oi_b3_intent_selector";
    internal const string CompletionClaim = "oi_b3_completion_selector";
    private const int PairFields = 2;
    private const int MultiplicityProbe = PairFields + 1;

    internal string Intent
    {
        get;
    }
    internal string Completion
    {
        get;
    }
    internal string IntentDigest
    {
        get;
    }

    /// <summary>Hashes decoded original bytes, not the ASCII selector spelling used by older ceremony-correlation digests.</summary>
    private BrowserLoginBinding(string intent, string completion)
    {
        Intent = intent;
        Completion = completion;
        IntentDigest = BrowserLoginSelector.Digest(intent)!;
    }

    /// <summary>Inspects original HTTP cardinality before native normalization can discard empty fields or collapse arrays.</summary>
    internal static BrowserLoginBindingState Read(IEnumerable<KeyValuePair<string, StringValues>> fields, out BrowserLoginBinding? binding) =>
        ReadValues(fields.Select(pair => KeyValuePair.Create(pair.Key, pair.Value.Count == 1 ? pair.Value[0] : null)),
            IntentParameter, CompletionParameter, out binding);

    /// <summary>Reads scalar native representation only; the caller still owns raw input, endpoint, client and authenticated-context validation.</summary>
    internal static BrowserLoginBindingState Read(OpenIddictRequest request, out BrowserLoginBinding? binding) =>
        ReadValues(request.GetParameters().Select(pair => KeyValuePair.Create(pair.Key,
            (System.Text.Json.JsonElement)pair.Value is { ValueKind: System.Text.Json.JsonValueKind.String } value ? value.GetString() : null)),
            IntentParameter, CompletionParameter, out binding);

    /// <summary>Reads one unambiguous private representation, without interpreting it as an authority receipt.</summary>
    internal static BrowserLoginBindingState Read(ClaimsPrincipal principal, out BrowserLoginBinding? binding)
    {
        binding = null;
        if (principal.Identities.ToArray() is not [var identity] || identity.Actor is not null)
        {
            return BrowserLoginBindingState.Invalid;
        }
        return ReadValues(identity.Claims.Select(claim => KeyValuePair.Create(claim.Type,
            claim.ValueType == ClaimValueTypes.String ? claim.Value : null)), IntentClaim, CompletionClaim, out binding);
    }

    /// <summary>Copies private data only into an unbound principal; refuses overwrite, ambiguity and actors, and adds no public destinations.</summary>
    internal void CopyTo(ClaimsPrincipal principal)
    {
        if (Read(principal, out _) != BrowserLoginBindingState.Absent || principal.Identity is not ClaimsIdentity identity)
        {
            throw new InvalidOperationException();
        }
        identity.AddClaim(new Claim(IntentClaim, Intent));
        identity.AddClaim(new Claim(CompletionClaim, Completion));
    }

    /// <summary>Compares against the original immutable transport snapshot; this is a representation check, not a durable completion check.</summary>
    internal bool Matches(ClaimsPrincipal principal) => Read(principal, out var current) == BrowserLoginBindingState.Present &&
        string.Equals(Intent, current!.Intent, StringComparison.Ordinal) && string.Equals(Completion, current.Completion, StringComparison.Ordinal);

    /// <summary>Distinguishes missing B2 data from malformed B3 data so partial pairs cannot silently downgrade to an absent binding.</summary>
    private static BrowserLoginBindingState ReadValues(IEnumerable<KeyValuePair<string, string?>> input, string intentName,
        string completionName, out BrowserLoginBinding? binding)
    {
        binding = null;
        var fields = input.Where(pair => string.Equals(pair.Key, intentName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(pair.Key, completionName, StringComparison.OrdinalIgnoreCase)).Take(MultiplicityProbe).ToArray();
        if (fields.Length == 0)
        {
            return BrowserLoginBindingState.Absent;
        }
        var intent = fields.FirstOrDefault(pair => pair.Key == intentName).Value;
        var completion = fields.FirstOrDefault(pair => pair.Key == completionName).Value;
        if (fields.Length != PairFields || !Canonical(intent) || !Canonical(completion))
        {
            return BrowserLoginBindingState.Invalid;
        }
        binding = new BrowserLoginBinding(intent!, completion!);
        return BrowserLoginBindingState.Present;
    }

    /// <summary>Rejects alternate trailing pad bits and every noncanonical spelling before hashing or future persistence lookup.</summary>
    private static bool Canonical(string? value) => BrowserLoginSelector.Digest(value) is not null;
}

/// <summary>Separates representation absence, malformed presence and a complete canonical pair without granting authentication.</summary>
internal enum BrowserLoginBindingState
{
    Absent, Invalid, Present
}
