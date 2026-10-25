namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Retains an encrypted committed generation for final publication without retaining a scoped protector or mutable database row.</summary>
internal sealed class BrowserLogoutCookie(string value, long expires)
{
    /// <summary>Recomputes browser lifetime only after every optional remote wait, preserving the original incoming publication bounds.</summary>
    internal void Publish(HttpContext http, TimeProvider clock)
    {
        http.Features.Get<BrowserLoginPublication>()?.Require(clock);
        var options = BrowserSessionCookies.Bounded(clock.GetUtcNow(), new DateTimeOffset(expires, TimeSpan.Zero))
            ?? throw new InvalidOperationException(BrowserLoginBoundary.Unavailable);
        http.Response.Cookies.Append(BrowserSessionCookies.Generation, value, options);
    }
}
