namespace IdentityPlatform.Issuer.Tests.Fixtures;

// No CookieContainer merging: observe the actual explicit header passed to the trusted TLS transport.
public sealed class ObserveTokenCookie(string expectedCookie)
    : DelegatingHandler(new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false })
{
    public int Attempts
    {
        get; private set;
    }
    public bool Matched
    {
        get; private set;
    }

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Attempts++;
        Matched = request.Method == HttpMethod.Post && request.RequestUri?.AbsolutePath == "/connect/token" &&
            request.Headers.TryGetValues("Cookie", out var values) && values.SequenceEqual([expectedCookie]);
        return base.SendAsync(request, cancellationToken);
    }
}
