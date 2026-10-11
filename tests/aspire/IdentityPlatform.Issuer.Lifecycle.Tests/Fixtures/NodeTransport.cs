using System.Net;
using System.Net.Sockets;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

// Test-only socket routing. TLS, request authority and browser protection remain canonical.
internal sealed class NodeTransport : HttpMessageHandler
{
    private readonly Uri issuer;
    private readonly Uri relyingParty;
    private readonly HttpMessageInvoker transport;
    private readonly bool redirects;
    private readonly Action? requireGeneration;
    private int issuerRequests;
    internal int IssuerRequests => Volatile.Read(ref issuerRequests);

    internal NodeTransport(Uri issuer, Uri relyingParty, int nodePort, CookieContainer cookies, bool redirects, Action? requireGeneration = null)
    {
        RequireOrigin(issuer);
        RequireOrigin(relyingParty);
        Assert.True(issuer.Authority != relyingParty.Authority && nodePort is > 0 and <= 65535, "Invalid transport topology.");
        this.issuer = issuer;
        this.relyingParty = relyingParty;
        this.redirects = redirects;
        this.requireGeneration = requireGeneration;
        transport = new HttpMessageInvoker(new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            CookieContainer = cookies,
            ConnectTimeout = TimeSpan.FromSeconds(5),
            ConnectCallback = async (context, cancellationToken) =>
            {
                var uri = context.InitialRequestMessage.RequestUri!;
                Validate(uri);
                Assert.True(context.DnsEndPoint.Host == uri.Host && context.DnsEndPoint.Port == uri.Port, "Unexpected TLS authority.");
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(IPAddress.Loopback, SameOrigin(uri, issuer) ? nodePort : relyingParty.Port, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        });
    }

    internal static void RequireOrigin(Uri uri) => Assert.True(uri.IsAbsoluteUri && uri.Scheme == Uri.UriSchemeHttps &&
        uri.IsLoopback && uri.UserInfo.Length == 0 && uri.AbsolutePath == "/" && uri.Query.Length == 0 && uri.Fragment.Length == 0,
        "Transport origins must be exact loopback HTTPS origins.");

    internal static bool SameOrigin(Uri left, Uri right) => left.Scheme == right.Scheme && left.Host == right.Host && left.Port == right.Port;

    private void Validate(Uri uri) => Assert.True(uri.IsAbsoluteUri && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 &&
        (SameOrigin(uri, issuer) || SameOrigin(uri, relyingParty)), "Request escaped the two allowed HTTPS origins.");

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var current = request;
        var owned = new List<HttpRequestMessage>();
        try
        {
            for (var hop = 0; hop < 12; hop++)
            {
                Validate(current.RequestUri!); // Also applies to reused sockets and every redirect.
                Assert.True(current.Headers.Host is null || current.Headers.Host == current.RequestUri!.Authority, "Host override is forbidden.");
                current.Version = HttpVersion.Version11;
                current.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
                if (SameOrigin(current.RequestUri!, issuer))
                {
                    requireGeneration?.Invoke();
                    Interlocked.Increment(ref issuerRequests);
                }
                var response = await transport.SendAsync(current, cancellationToken);
                if (!redirects || response.Headers.Location is not { } location ||
                    response.StatusCode is not (HttpStatusCode.Redirect or HttpStatusCode.SeeOther or HttpStatusCode.MovedPermanently))
                {
                    return response;
                }
                var target = location.IsAbsoluteUri ? location : new Uri(current.RequestUri!, location);
                response.Dispose();
                Validate(target);
                Assert.True(current.Method == HttpMethod.Get || current.Method == HttpMethod.Post, "Unexpected redirect method.");
                // Browser 301/302/303 behavior; never replay a form or token body automatically.
                current = new HttpRequestMessage(HttpMethod.Get, target);
                owned.Add(current);
            }
            throw new InvalidOperationException("Test navigation exceeded its redirect bound.");
        }
        finally
        {
            foreach (var message in owned)
            {
                message.Dispose();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            transport.Dispose();
        }
        base.Dispose(disposing);
    }
}
