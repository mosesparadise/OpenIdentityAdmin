using System.Net;
using System.Text.Json;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Checks private child output without allowing regression diagnostics to print cookies, redirects or native antiforgery HTML.</summary>
internal static class BrowserProcessAssertions
{
    private const string Authenticated = "authenticated";
    private const string Subject = "subject";
    private const string Committed = "session has ended locally";
    private const string IssuerUnrequested = "Issuer sign-out was not requested.";
    private const string PrivateFailure = "Private browser process output did not satisfy the required contract; values withheld.";

    /// <summary>Checks actual minimal JSON output, rather than manufacturing an authentication principal in the parent or child.</summary>
    internal static void RequireStatus(NativeClientProcessReply reply, bool authenticated, string? subject = null)
    {
        RequireLocal(reply, HttpStatusCode.OK);
        using var json = JsonDocument.Parse(reply.BrowserBody!);
        Assert.Equal(authenticated, json.RootElement.GetProperty(Authenticated).GetBoolean());
        if (authenticated)
        {
            Assert.True(subject == json.RootElement.GetProperty(Subject).GetString(), PrivateFailure);
        }
        else
        {
            Assert.True(json.RootElement.EnumerateObject().Count() == 1, PrivateFailure);
        }
        Assert.True(reply.Cookies.Length == 0, PrivateFailure);
    }

    /// <summary>Requires successful nonaborted local completion with no redirect or even attempted issuer HTTP.</summary>
    internal static void RequireLocal(NativeClientProcessReply reply, HttpStatusCode status)
    {
        Assert.Equal((int)status, reply.Status);
        Assert.False(reply.Aborted);
        Assert.Equal(0, reply.OutboundRequests);
        Assert.True(reply.Location.Length == 0, PrivateFailure);
        Assert.NotNull(reply.BrowserBody);
    }

    /// <summary>Requires both truthful outcome statements through fixed-message predicates, never including unexpected secret-bearing HTML in TRX.</summary>
    internal static void RequireCommitted(NativeClientProcessReply reply)
    {
        Assert.True(reply.BrowserBody?.Contains(Committed, StringComparison.Ordinal) == true, PrivateFailure);
        Assert.True(reply.BrowserBody?.Contains(IssuerUnrequested, StringComparison.Ordinal) == true, PrivateFailure);
    }
}
