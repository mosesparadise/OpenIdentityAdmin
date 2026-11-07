namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Binds one observed native submission to its own clean terminal event independently of document navigation.</summary>
internal sealed class CrossSitePostReceipt
{
    private readonly object sync = new();
    private readonly TaskCompletionSource terminal = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private object? submitted;
    private object? ended;
    private int submissions;
    private int endings;
    private bool clean;
    private bool ordered;
    internal Task Terminal => terminal.Task;

    /// <summary>Records every matching submission, preserving the first actual request identity.</summary>
    internal void Submit(object request)
    {
        lock (sync)
        {
            submissions++;
            submitted ??= request;
        }
    }

    /// <summary>Keeps failed, premature and duplicate terminal events distinguishable from one clean completion.</summary>
    internal void End(object request, bool finished)
    {
        lock (sync)
        {
            endings++;
            ended = request;
            clean = finished;
            ordered = submissions == 1 && ReferenceEquals(submitted, request);
            terminal.TrySetResult();
        }
    }

    /// <summary>Accepts only the response's same single request after its observed clean completion.</summary>
    internal bool Accepts(object request)
    {
        lock (sync)
        {
            return submissions == 1 && endings == 1 && clean && ordered &&
                ReferenceEquals(submitted, request) && ReferenceEquals(ended, request);
        }
    }

    /// <summary>Calibrates missing, premature, failed, duplicated and unrelated terminal evidence without timers or browser mutation.</summary>
    internal static void RequireControls()
    {
        var request = new object();
        var receipt = new CrossSitePostReceipt();
        Assert.False(receipt.Terminal.IsCompleted);
        Assert.False(receipt.Accepts(request));
        receipt.Submit(request);
        Assert.False(receipt.Terminal.IsCompleted);
        Assert.False(receipt.Accepts(request));
        receipt.End(request, finished: true);
        Assert.True(receipt.Terminal.IsCompletedSuccessfully && receipt.Accepts(request));
        Assert.False(receipt.Accepts(new object()));
        receipt.End(request, finished: true);
        Assert.False(receipt.Accepts(request));
        RequireRejected(request, before: true, failed: false, duplicate: false, unrelated: false);
        RequireRejected(request, before: false, failed: true, duplicate: false, unrelated: false);
        RequireRejected(request, before: false, failed: false, duplicate: true, unrelated: false);
        RequireRejected(request, before: false, failed: false, duplicate: false, unrelated: true);
    }

    /// <summary>Exercises independent malformed event histories against the same receipt consumed by the real browser witness.</summary>
    private static void RequireRejected(object request, bool before, bool failed, bool duplicate, bool unrelated)
    {
        var receipt = new CrossSitePostReceipt();
        if (before)
        {
            receipt.End(request, finished: true);
        }
        receipt.Submit(request);
        if (duplicate)
        {
            receipt.Submit(request);
        }
        if (!before)
        {
            receipt.End(unrelated ? new object() : request, finished: !failed);
        }
        Assert.True(receipt.Terminal.IsCompletedSuccessfully);
        Assert.False(receipt.Accepts(request));
    }
}
