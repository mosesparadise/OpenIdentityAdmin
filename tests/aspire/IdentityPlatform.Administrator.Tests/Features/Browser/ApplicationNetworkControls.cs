namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Seeds native correlation failures before trusting a browser's actual network observations.</summary>
internal static class ApplicationNetworkControls
{
    private const string Frame = "owned-frame";
    private const string Loader = "owned-loader";
    private const string Target = "https://example.invalid/bff/api/v1/applications";
    private const string OtherTarget = "https://example.invalid/elsewhere";
    private const string Request = "owned-request";
    private const string Other = "unrelated";
    private const string Post = "POST";
    private const string Get = "GET";

    /// <summary>Requires positive finish/abort controls and rejects missing, wrong, duplicate, redirected, malformed and disconnected evidence.</summary>
    internal static void Require()
    {
        foreach (var failed in new[] { false, true })
        {
            var valid = Started();
            valid.Ended(Other, failed);
            Assert.False(valid.Completion.IsCompleted);
            valid.Ended(Request, failed);
            Assert.True(valid.IsComplete && valid.Completion.IsCompleted && valid.Failed == failed);
            valid.Ended(Request, failed);
            Assert.False(valid.IsComplete);
            Assert.False(valid.Seal());
        }
        var absent = new ApplicationNetworkLedger(Frame, Loader, Target);
        absent.Ended(Request, false);
        Assert.False(absent.IsComplete || absent.Completion.IsCompleted);
        var pending = Started();
        Assert.False(pending.IsComplete || pending.Completion.IsCompleted);
        pending.Invalidate();
        Assert.False(pending.IsComplete);
        RequireBadStarts();
        RequireReuse();
        var sealedValue = Started();
        sealedValue.Ended(Request, false);
        Assert.True(sealedValue.Seal());
        Assert.False(sealedValue.Seal());
    }

    /// <summary>Rejects provenance mismatches and malformed identifiers while ignoring unrelated native traffic.</summary>
    private static void RequireBadStarts()
    {
        (string? Id, string Frame, string Loader, bool Redirect)[] bad = [(null, Frame, Loader, false),
            (string.Empty, Frame, Loader, false), (new string('x', ApplicationNetworkLedger.MaximumIdentifier + 1), Frame, Loader, false),
            (Request, Other, Loader, false), (Request, Frame, Other, false), (Request, Frame, Loader, true)];
        foreach (var item in bad)
        {
            var value = new ApplicationNetworkLedger(Frame, Loader, Target);
            value.Started(item.Id, item.Frame, item.Loader, Target, Post, item.Redirect);
            value.Ended(item.Id, false);
            Assert.False(value.IsComplete);
        }
        var unrelated = new ApplicationNetworkLedger(Frame, Loader, Target);
        unrelated.Started(Other, Frame, Loader, OtherTarget, Post, false);
        unrelated.Started(Other, Frame, Loader, Target, Get, false);
        unrelated.Ended(Other, false);
        Assert.True(unrelated.Requests == 0 && !unrelated.IsComplete && !unrelated.Completion.IsCompleted);
    }

    /// <summary>Duplicate dispatch and identifier reuse through a different redirect destination cannot satisfy the original request.</summary>
    private static void RequireReuse()
    {
        var duplicate = Started();
        duplicate.Ended(Request, false);
        duplicate.Started(Other, Frame, Loader, Target, Post, false);
        Assert.False(duplicate.IsComplete);
        Assert.False(duplicate.Seal());
        var redirect = Started();
        redirect.Ended(Request, false);
        redirect.Started(Request, Frame, Loader, OtherTarget, Get, true);
        Assert.False(redirect.IsComplete);
        Assert.False(redirect.Seal());
        var closed = Started();
        closed.Ended(Request, false);
        closed.Invalidate();
        Assert.False(closed.IsComplete);
        Assert.False(closed.Seal());
    }

    /// <summary>Uses independently fixed synthetic provenance for every control.</summary>
    private static ApplicationNetworkLedger Started()
    {
        var value = new ApplicationNetworkLedger(Frame, Loader, Target);
        value.Started(Request, Frame, Loader, Target, Post, false);
        return value;
    }
}
