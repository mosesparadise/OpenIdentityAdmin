using System.Globalization;
using System.Net;
using System.Net.Security;
using System.Net.Sockets;
using System.Text;

namespace IdentityPlatform.Administrator.Tests.Fixtures;

/// <summary>Sends one real validated TLS request and never reads or applies its HTTP response.</summary>
internal sealed class UnreceivedResponse : IAsyncDisposable
{
    private static readonly CompositeFormat Request = CompositeFormat.Parse("POST {0} HTTP/1.1\r\nHost: {1}\r\nCookie: {2}\r\nContent-Type: application/x-www-form-urlencoded\r\nContent-Length: {3}\r\nConnection: close\r\n\r\n");
    private const int DeadlineSeconds = 10;
    private readonly TcpClient connection = new();
    private SslStream? stream;

    /// <summary>Preserves the original cookie state and writes the native form without receiving headers, cookies or recovery text.</summary>
    internal async Task SendAsync(Uri issuer, CookieContainer cookies, AdministratorForm form, string code, string path = AdministratorJourney.Enrollment)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(DeadlineSeconds));
        await connection.ConnectAsync(issuer.Host, issuer.Port, deadline.Token);
        stream = new SslStream(connection.GetStream());
        await stream.AuthenticateAsClientAsync(new SslClientAuthenticationOptions { TargetHost = issuer.Host }, deadline.Token);
        using var content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            [AdministratorJourney.Interaction] = form.Interaction,
            [AdministratorJourney.Antiforgery] = form.Antiforgery,
            [AdministratorJourney.CodeField] = code
        });
        var body = await content.ReadAsByteArrayAsync(deadline.Token);
        var headers = Encoding.ASCII.GetBytes(string.Format(CultureInfo.InvariantCulture, Request,
            path, issuer.Authority, cookies.GetCookieHeader(issuer), body.Length));
        await stream.WriteAsync(headers, deadline.Token);
        await stream.WriteAsync(body, deadline.Token);
        await stream.FlushAsync(deadline.Token);
    }

    /// <summary>Drops the connection after the caller independently observes commit, without draining a secret response.</summary>
    public async ValueTask DisposeAsync()
    {
        try
        {
            if (stream is not null)
            {
                await stream.DisposeAsync();
            }
        }
        finally { connection.Dispose(); }
    }
}
