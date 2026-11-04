namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Correlates one native main-document POST with one terminal signal without retaining protocol payloads.</summary>
internal sealed class ApplicationNetworkLedger(string frame, string loader, string target)
{
    private const string Post = "POST";
    private const int DuplicateCount = 2;
    internal const int MaximumIdentifier = 128;
    private readonly TaskCompletionSource completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly object gate = new();
    private string? request;
    private bool invalid;
    private bool terminal;
    private bool sealedObservation;
    private int requests;
    private bool failed;
    internal Task Completion => completion.Task;
    internal int Requests
    {
        /// <summary>Reads the synchronized initiation count, capped at the duplicate sentinel.</summary>
        get
        {
            lock (gate)
            {
                return requests;
            }
        }
    }
    internal bool Failed
    {
        /// <summary>Reads the captured terminal outcome without exposing the browser's native error text.</summary>
        get
        {
            lock (gate)
            {
                return failed;
            }
        }
    }
    internal bool IsComplete
    {
        /// <summary>Reads a coherent completion snapshot that includes late invalidation.</summary>
        get
        {
            lock (gate)
            {
                return Complete;
            }
        }
    }
    private bool Initiated => !invalid && requests == 1 && request is not null;
    private bool Complete => Initiated && terminal;

    /// <summary>Rejects duplicate dispatch, redirect reuse and wrong document provenance before accepting the unique request identifier.</summary>
    internal void Started(string? id, string? actualFrame, string? actualLoader, string? url, string? method, bool redirected)
    {
        lock (gate)
        {
            StartCore(id, actualFrame, actualLoader, url, method, redirected);
        }
    }

    /// <summary>Serializes native initiation with final observation sealing.</summary>
    private void StartCore(string? id, string? actualFrame, string? actualLoader, string? url, string? method, bool redirected)
    {
        if (sealedObservation || request is not null && request == id)
        {
            Invalidate();
            return;
        }
        if (url != target || method != Post)
        {
            return;
        }
        requests = Math.Min(DuplicateCount, requests + 1);
        if (requests != 1 || !Identifier(id) || actualFrame != frame || actualLoader != loader || redirected)
        {
            Invalidate();
            return;
        }
        request = id;
    }

    /// <summary>Only a terminal event for the captured identifier can complete the witness; unrelated signals cannot stand in for it.</summary>
    internal void Ended(string? id, bool failed)
    {
        lock (gate)
        {
            EndCore(id, failed);
        }
    }

    /// <summary>Serializes terminal evidence with invalidation and final observation sealing.</summary>
    private void EndCore(string? id, bool failed)
    {
        if (sealedObservation)
        {
            Invalidate();
            return;
        }
        if (request is null || request != id)
        {
            return;
        }
        if (terminal)
        {
            Invalidate();
            return;
        }
        terminal = true;
        this.failed = failed;
        completion.TrySetResult();
    }

    /// <summary>Disconnection or malformed owned evidence fails closed instead of being treated as network completion.</summary>
    internal void Invalidate()
    {
        lock (gate)
        {
            invalid = true;
            completion.TrySetResult();
        }
    }

    /// <summary>Rechecks all late evidence after the owner freezes subscriptions; a second seal cannot create another acceptance.</summary>
    internal bool Seal(bool requireTerminal = true)
    {
        lock (gate)
        {
            if (sealedObservation)
            {
                invalid = true;
            }
            sealedObservation = true;
            return requireTerminal ? Complete : Initiated;
        }
    }

    /// <summary>Bounds opaque native identifiers independently of their contents.</summary>
    internal static bool Identifier(string? value) => value is { Length: > 0 and <= MaximumIdentifier };
}
