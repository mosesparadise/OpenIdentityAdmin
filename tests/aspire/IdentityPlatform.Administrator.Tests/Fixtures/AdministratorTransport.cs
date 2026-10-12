using System.Net;
using System.Net.Sockets;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Routes canonical account HTTPS requests to a selected real node without bypassing certificate validation.</summary>
internal sealed class AdministratorTransport : DelegatingHandler
{
    private const int MaximumPort = 65535;
    private const string RootPath = "/";
    private const int ConnectSeconds = 5;
    private const string InvalidOrigin = "Request escaped canonical account origin.";

    private readonly Uri issuer;

    /// <summary>Creates bounded direct loopback sockets while preserving TLS SNI, URL authority and browser cookie scope.</summary>
    internal AdministratorTransport(Uri issuer, int port, CookieContainer cookies)
    {
        Assert.True(issuer.Scheme == Uri.UriSchemeHttps && issuer.IsLoopback && issuer.AbsolutePath == RootPath &&
            issuer.UserInfo.Length == 0 && issuer.Query.Length == 0 && port is > 0 and <= MaximumPort);
        this.issuer = issuer;
        InnerHandler = new SocketsHttpHandler
        {
            UseProxy = false,
            AllowAutoRedirect = false,
            CookieContainer = cookies,
            ConnectTimeout = TimeSpan.FromSeconds(ConnectSeconds),
            ConnectCallback = async (context, token) =>
            {
                Require(context.InitialRequestMessage);
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                try
                {
                    await socket.ConnectAsync(IPAddress.Loopback, port, token);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch { socket.Dispose(); throw; }
            }
        };
    }

    /// <summary>Rejects origin changes and Host overrides before both new and reused connections.</summary>
    private void Require(HttpRequestMessage request) => Assert.True(request.RequestUri is { } uri &&
        uri.Scheme == issuer.Scheme && uri.Authority == issuer.Authority && uri.UserInfo.Length == 0 && uri.Fragment.Length == 0 &&
        (request.Headers.Host is null || request.Headers.Host == issuer.Authority), InvalidOrigin);

    /// <summary>Validates each request independently and never follows a redirect with a secret-bearing body.</summary>
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        Require(request);
        request.Version = HttpVersion.Version11;
        request.VersionPolicy = HttpVersionPolicy.RequestVersionExact;
        return base.SendAsync(request, cancellationToken);
    }
}
