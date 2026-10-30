using System.Security.Cryptography;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;

namespace IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;

/// <summary>Rejects post-render callback or cleanup mutation of managed account output without retaining printable proof-bearing headers or body.</summary>
internal sealed class ManagedResponseFreeze(ProtocolResponseCapture capture)
{
    private readonly string expected = Binding(capture);

    /// <summary>Repeats the exact rendered-output check after sealing, actual disposal and before live response publication.</summary>
    internal void Require()
    {
        if (!capture.IsSealed || expected != Binding(capture))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Hashes finite output metadata and bytes; no header, credential or form value is printed.</summary>
    private static string Binding(ProtocolResponseCapture value) => FactorBinding.Digest(new
    {
        value.StatusCode,
        value.ReasonPhrase,
        Headers = value.Headers.OrderBy(pair => pair.Key, StringComparer.Ordinal).Select(pair => new { pair.Key, Values = pair.Value.ToArray() }),
        Body = Convert.ToHexString(SHA256.HashData(value.Bytes.Span))
    });
}
