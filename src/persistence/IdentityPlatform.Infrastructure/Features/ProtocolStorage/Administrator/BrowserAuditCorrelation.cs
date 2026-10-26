using Microsoft.Extensions.Primitives;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

/// <summary>Defines non-authorizing audit provenance carried only on the authenticated native PAR backchannel.</summary>
internal static class BrowserAuditCorrelation
{
    internal const string Format = "N";
    internal const string Header = "Identity-Browser-Audit-Correlation";

    /// <summary>Accepts one nonempty canonical lowercase GUID; duplicate, combined and coercible values fail closed.</summary>
    internal static Guid? Read(StringValues values) => values is { Count: 1 } && values[0] is { } value &&
        Guid.TryParseExact(value, Format, out var correlation) && correlation != Guid.Empty &&
        value == correlation.ToString(Format) ? correlation : null;
}
