using System.Net;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

// A real TLS request commits at the issuer, but its response never reaches application code.
public sealed class DiscardTokenResponse() : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
{
    private int attempts;
    public int Attempts => Volatile.Read(ref attempts);
    public bool SuccessfulResponseDiscarded
    {
        get; private set;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Interlocked.Increment(ref attempts);
        bool successful;
        using (var response = await base.SendAsync(request, cancellationToken))
        {
            successful = response.StatusCode == HttpStatusCode.OK;
            await response.Content.CopyToAsync(Stream.Null, cancellationToken);
        }
        SuccessfulResponseDiscarded = successful;
        throw new HttpRequestException("Synthetic token-response delivery failure.");
    }
}
