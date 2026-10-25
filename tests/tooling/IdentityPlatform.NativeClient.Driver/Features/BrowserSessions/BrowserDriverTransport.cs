namespace IdentityPlatform.NativeClient.Driver.Features.BrowserSessions;

/// <summary>Counts and rejects every attempted external request; local status and logout must remain independent of issuer transport.</summary>
internal sealed class BrowserDriverTransport
{
    private int sends;
    internal int Sends => Volatile.Read(ref sends);

    /// <summary>Creates an independently owned handler while retaining one whole-child request counter.</summary>
    internal DelegatingHandler Handler() => new DenyHandler(this);

    /// <summary>Prevents even an attempted metadata request from reaching a transport socket.</summary>
    private sealed class DenyHandler(BrowserDriverTransport owner) : DelegatingHandler
    {
        private const string Failure = "Browser child transport is unavailable.";

        /// <summary>Records one attempted send without exposing its URI, headers or credentials.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref owner.sends);
            throw new InvalidOperationException(Failure);
        }
    }
}
