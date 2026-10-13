using System.Text.Json;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Defines bounded synthetic parser inputs independently of the driver DTO and transport constants.</summary>
internal static class DriverTransportCases
{
    internal const string PostgreSql = "PostgreSql";
    internal const string SqlServer = "SqlServer";
    internal const string Boundary = "valid-input-composition-boundary";
    internal const string Malformed = "malformed-json";
    internal const string Duplicate = "duplicate-root-property";
    internal const string NestedDuplicate = "duplicate-issuer-property";
    internal const string Oversized = "oversized-whitespace-frame";
    internal const string Depth = "excessive-json-depth";
    internal const string Incomplete = "closed-without-newline";
    internal const string Unknown = "unknown-root-property";
    internal static readonly string[] Inputs = [Boundary, Malformed, Duplicate, NestedDuplicate, Oversized, Depth, Incomplete, Unknown];
    private const int FrameBytes = 65_536;
    private const int ExcessDepth = 9;
    private const string Canary = "synthetic-private-transport-canary";
    private const string Read = "Read";
    private const string Actor = "synthetic-actor";
    private const string Newline = "\n";
    private const string DuplicateRoot = ",\"Operation\":\"Read\"}";
    private const string UnknownRoot = ",\"UnrecognizedPrivateInput\":\"synthetic-private-transport-canary\"}";
    private const string NestedStart = ",\"UnrecognizedPrivateInput\":";
    private const string DepthProperty = "{\"nested\":";
    private const string IssuerProperty = "\"Identity:Issuer\":\"synthetic-private-transport-canary\"";
    private const string Separator = ",";
    private static readonly Dictionary<string, string> Issuer = new(StringComparer.Ordinal)
    {
        ["Identity:Issuer"] = Canary,
        ["Identity:Protection:DataProtection:KeyRingPath"] = Canary,
        ["Identity:Protection:Signing:Path"] = Canary,
        ["Identity:Protection:Signing:Password"] = Canary,
        ["Identity:Protection:Encryption:Path"] = Canary,
        ["Identity:Protection:Encryption:Password"] = Canary,
        ["Identity:Protection:DataProtection:Current:Path"] = Canary,
        ["Identity:Protection:DataProtection:Current:Password"] = Canary
    };

    /// <summary>Produces finite reviewed framing/schema cases from an otherwise accepted input shape; connection configuration remains deliberately invalid.</summary>
    internal static string Create(string provider, string condition)
    {
        var baseline = JsonSerializer.Serialize(new
        {
            Operation = Read,
            Provider = provider,
            RuntimeConnection = Canary,
            IssuerSettings = Issuer,
            ActorId = Actor
        });
        return condition switch
        {
            Boundary => baseline + Newline,
            Malformed => baseline[..^1] + Newline,
            Duplicate => baseline[..^1] + DuplicateRoot + Newline,
            NestedDuplicate => baseline.Replace(IssuerProperty, IssuerProperty + Separator + IssuerProperty, StringComparison.Ordinal) + Newline,
            Oversized => baseline.PadRight(FrameBytes, ' ') + Newline,
            Depth => baseline[..^1] + NestedStart + string.Concat(Enumerable.Repeat(DepthProperty, ExcessDepth)) +
                JsonSerializer.Serialize(Canary) + new string('}', ExcessDepth + 1) + Newline,
            Incomplete => baseline,
            Unknown => baseline[..^1] + UnknownRoot + Newline,
            _ => throw new ArgumentOutOfRangeException(nameof(condition))
        };
    }
}
