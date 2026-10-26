namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Seeds independent cookie parsing controls before any actual transient-value absence claim.</summary>
internal static class BrowserFlowControls
{
    private const string Query = "https://localhost/callback?code=probe-code&state=probe-state&Completion=probe-completion";
    private const string Cookie = "CoOkIe";
    private const string Native = "__Host-identity-admin-client-state=probe-native";
    private const string Prelogin = "__Host-identity-admin-prelogin=probe-prelogin";
    private const string Malformed = "; =broken";
    private const string SetCookie = "set-cookie";

    /// <summary>Requires complete atomic parsing, both nonempty classes, case-insensitive header recognition and unchanged response-parser rejection.</summary>
    internal static void Require()
    {
        var browser = QueryFlow();
        browser.RequestHeader(Cookie, Native + Separator + Prelogin + Unrelated);
        Assert.True(browser.Require().ToHashSet().SetEquals(Expected));
        var witnessed = QueryFlow();
        witnessed.RequestHeader(Cookie, Native + Separator + Prelogin, witnessed: true);
        Assert.True(witnessed.Require().ToHashSet().SetEquals(Expected));
        Assert.Throws<FormatException>(() => browser.Header(SetCookie, InvalidResponse));
        var partial = QueryFlow();
        partial.RequestHeader(Cookie, Prelogin);
        Assert.Throws<FormatException>(() => partial.RequestHeader(Cookie, Native + Malformed));
        Assert.Throws<Xunit.Sdk.TrueException>(() => partial.Require());
        var empty = QueryFlow();
        empty.RequestHeader(Cookie, EmptyNative + Separator + Prelogin);
        Assert.Throws<Xunit.Sdk.TrueException>(() => empty.Require());
    }

    /// <summary>Seeds the three independent query values without supplying either mandatory cookie class.</summary>
    private static BrowserFlowSecrets QueryFlow()
    {
        var flow = new BrowserFlowSecrets();
        flow.Request(Query);
        return flow;
    }
    private const string Separator = "; ";
    private const string Unrelated = "; unrelated=probe-unrelated";
    private const string EmptyNative = "__Host-identity-admin-client-state=";
    private const string InvalidResponse = "=invalid";
    private static readonly string[] Expected = ["probe-code", "probe-state", "probe-completion", "probe-native", "probe-prelogin"];
}
