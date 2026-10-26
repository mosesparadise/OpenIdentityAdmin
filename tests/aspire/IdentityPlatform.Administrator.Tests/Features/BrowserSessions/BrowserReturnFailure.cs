using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Aborts only the genuine fixed return transport before product middleware, retaining no HTTP context or secret diagnostics.</summary>
internal sealed class BrowserReturnFailure(string path = "/bff/logout/return")
{
    private int armed;
    private int count;
    private Uri? destination;
    internal int Count => Volatile.Read(ref count);
    internal Uri? Destination => Volatile.Read(ref destination);

    /// <summary>Starts one bounded failure window; every browser retry stays blocked until the witnessed navigation failure is complete.</summary>
    internal void Arm() => Assert.Equal(0, Interlocked.Exchange(ref armed, 1));

    /// <summary>Restores transport only for a later explicit original return, never resubmitting the failed navigation.</summary>
    internal void Disarm() => Assert.Equal(1, Interlocked.Exchange(ref armed, 0));

    /// <summary>Records each actual return GET privately and aborts its connection without executing any product handler or fabricating a response.</summary>
    internal bool Abort(HttpContext http, Uri address)
    {
        if (Volatile.Read(ref armed) == 0 || !HttpMethods.IsGet(http.Request.Method) || http.Request.Path != path)
        {
            return false;
        }
        var actual = new Uri(address, http.Request.Path + http.Request.QueryString);
        var prior = Interlocked.CompareExchange(ref destination, actual, null);
        Assert.True(prior is null || prior == actual);
        Interlocked.Increment(ref count);
        http.Abort();
        return true;
    }
}
