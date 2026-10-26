using System.Net.Http.Headers;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Keeps audit metadata inside the exact private PAR permit rather than native parameters or client-wide defaults.</summary>
internal static class NativeClientAuditHeader
{
    /// <summary>Rejects caller-owned values before adding the acknowledged reservation's correlation to this request alone.</summary>
    internal static void Attach(HttpRequestHeaders message, HttpRequestHeaders defaults, Guid? correlation)
    {
        Require(message, defaults, null);
        if (correlation is { } value)
        {
            if (value == Guid.Empty)
            {
                throw ProtocolStorageOwner.Unavailable();
            }
            message.Add(BrowserAuditCorrelation.Header, value.ToString(BrowserAuditCorrelation.Format));
        }
    }

    /// <summary>Rechecks exact cardinality and value after asynchronous serialization; ordinary PAR and token sends require absence.</summary>
    internal static void Require(HttpRequestHeaders message, HttpRequestHeaders defaults, Guid? correlation)
    {
        var present = message.TryGetValues(BrowserAuditCorrelation.Header, out var values);
        if (defaults.Contains(BrowserAuditCorrelation.Header) || (correlation is { } expected
            ? !present || BrowserAuditCorrelation.Read(values!.ToArray()) != expected
            : present))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
