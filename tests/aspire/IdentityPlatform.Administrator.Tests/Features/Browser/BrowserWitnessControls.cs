using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Proves exact caller accounting and passive fail-closed behavior independently of actual HTTPS observations.</summary>
internal static class BrowserWitnessControls
{
    private const string BrowserPath = "/browser";
    private const string NativePath = "/native";
    private const string Host = "localhost";
    private const string Origin = "https://localhost";
    private const string Method = "GET";
    private const int Status = 200;

    /// <summary>Rejects missing, duplicated, unowned and ambiguously attributed responses while accepting the exact browser/native union.</summary>
    internal static void Require()
    {
        using var witness = new BrowserResponseWitness(() => { });
        witness.Observe(Context(BrowserPath));
        witness.Observe(Context(NativePath));
        var browser = BrowserResponseIdentity.Create(Method, Origin + BrowserPath, Status);
        Assert.Throws<Xunit.Sdk.TrueException>(() => witness.Require([browser]));
        witness.Native(Method, Origin + NativePath, Status);
        Assert.True(witness.Require([browser]).Headers.Length > 0);
        Assert.Throws<Xunit.Sdk.TrueException>(() => witness.Require([browser with { Method = Post }]));
        Assert.Throws<Xunit.Sdk.TrueException>(() => witness.Require([browser with { Address = Origin + OtherPath }]));
        Assert.Throws<Xunit.Sdk.TrueException>(() => witness.Require([browser with { Status = Created }]));
        Assert.Throws<Xunit.Sdk.TrueException>(() => witness.Require([browser, browser]));
        witness.Native(Method, Origin + BrowserPath, Status);
        Assert.Throws<Xunit.Sdk.FalseException>(() => witness.Require([browser]));
        RequirePassiveFailure();
        RequireBounds();
    }

    /// <summary>Rejects oversized native identities and excess browser observations even when all previously admitted responses match.</summary>
    private static void RequireBounds()
    {
        using var native = new BrowserResponseWitness(() => { });
        native.Native(new string(Fill, Oversized), Origin + NativePath, Status);
        var identity = BrowserResponseIdentity.Create(Method, Origin + BrowserPath, Status);
        Assert.Throws<Xunit.Sdk.EqualException>(() => native.Require([identity]));
        using var server = new BrowserResponseWitness(() => { });
        var browser = new BrowserResponseCapture();
        for (var index = 0; index < ResponseLimit; index++)
        {
            server.Observe(Context(BrowserPath));
            browser.Observe(identity);
        }
        Assert.Equal(ResponseLimit, browser.Require(server).Headers.Length);
        browser.Observe(identity);
        Assert.Throws<Xunit.Sdk.EqualException>(() => browser.Require(server));
    }

    /// <summary>Oversized observation returns normally and leaves the original headers intact, but later acceptance fails.</summary>
    private static void RequirePassiveFailure()
    {
        using var witness = new BrowserResponseWitness(() => { });
        var context = Context(BrowserPath);
        context.Response.Headers[TestHeader] = new string(Fill, Oversized);
        witness.Observe(context);
        Assert.Equal(Oversized, context.Response.Headers[TestHeader][0]!.Length);
        Assert.Throws<Xunit.Sdk.EqualException>(() => witness.Require([BrowserResponseIdentity.Create(Method, Origin + BrowserPath, Status)]));
    }

    /// <summary>Constructs a synthetic header record without invoking a database, network or product endpoint.</summary>
    private static DefaultHttpContext Context(string path)
    {
        var context = new DefaultHttpContext();
        context.Request.Scheme = Uri.UriSchemeHttps;
        context.Request.Host = new HostString(Host);
        context.Request.Method = Method;
        context.Request.Path = path;
        context.Response.StatusCode = Status;
        context.Response.Headers[TestHeader] = Synthetic;
        return context;
    }
    private const string TestHeader = "X-Synthetic-Witness";
    private const string Synthetic = "probe";
    private const int Oversized = 2097153;
    private const char Fill = 'x';
    private const int ResponseLimit = 256;
    private const string Post = "POST";
    private const string OtherPath = "/other";
    private const int Created = 201;
}
