namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Binds a stable observer to one original main-document context epoch and rejects restored ownership after native clearing.</summary>
internal sealed class ApplicationDocumentLedger(string frame)
{
    private readonly object gate = new();
    private readonly TaskCompletionSource ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource destroyed = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int originalId;
    private int currentId;
    private string? original;
    private string? current;
    private bool armed;
    private bool ended;
    private bool invalid;
    private bool sealedObservation;
    internal ApplicationDocumentEnd EndKind
    {
        get; private set;
    }
    internal Task Ready => ready.Task;
    internal Task Destroyed => destroyed.Task;
    internal bool IsDestroyed
    {
        /// <summary>Requires actual clearing and a different current default context, with no restored original owner.</summary>
        get
        {
            lock (gate)
            {
                return Valid;
            }
        }
    }
    private bool Valid => !invalid && armed && ended && original is not null && current is not null && current != original;

    /// <summary>Captures the first exact main/default context and never replaces its original identity with a later document.</summary>
    internal void Created(string? actualFrame, int id, string? unique, bool isDefault)
    {
        lock (gate)
        {
            if (!isDefault || actualFrame != frame)
            {
                return;
            }
            if (sealedObservation || id <= 0 || !ApplicationNetworkLedger.Identifier(unique))
            {
                Invalidate();
                return;
            }
            if (original is null)
            {
                original = unique;
                originalId = id;
            }
            else if (ended && unique == original || !ended && id == originalId && unique != original)
            {
                Invalidate();
            }
            current = unique;
            currentId = id;
            ready.TrySetResult();
            Signal();
        }
    }

    /// <summary>Arms only while the original captured context is still current, before the actual browser submission.</summary>
    internal void Arm()
    {
        lock (gate)
        {
            Assert.True(!invalid && !armed && !ended && original is not null && current == original);
            armed = true;
        }
    }

    /// <summary>Rejects an original owner lost before the navigation trigger; already-cleared ownership cannot satisfy a later boundary.</summary>
    internal void RequireLive()
    {
        lock (gate)
        {
            Assert.True(!invalid && armed && !ended && current == original);
        }
    }

    /// <summary>Accepts an explicit exact-context destruction or same-session clearing of all captured contexts, never session closure.</summary>
    internal void Cleared(int? id = null, string? unique = null)
    {
        lock (gate)
        {
            if (original is null)
            {
                return;
            }
            if (id is not null && (id != originalId || unique != original))
            {
                if (id == currentId && unique == current)
                {
                    current = null;
                }
                return;
            }
            if (!armed || sealedObservation)
            {
                Invalidate();
                return;
            }
            ended = true;
            EndKind |= id is null ? ApplicationDocumentEnd.ContextsCleared : ApplicationDocumentEnd.IndividualDestroyed;
            if (id is null || current == original)
            {
                current = null;
            }
            Signal();
        }
    }

    /// <summary>Completes only when clearing and a distinct current owner are both observed, or when invalidation must fail the waiter.</summary>
    private void Signal()
    {
        if (invalid || Valid)
        {
            destroyed.TrySetResult();
        }
    }

    /// <summary>Session loss, malformed owned evidence or observation resets cannot become a destruction witness.</summary>
    internal void Invalidate()
    {
        lock (gate)
        {
            invalid = true;
            ready.TrySetResult();
            destroyed.TrySetResult();
        }
    }

    /// <summary>Checks all late observations after the owner freezes event subscriptions.</summary>
    internal bool Seal()
    {
        lock (gate)
        {
            invalid |= sealedObservation;
            sealedObservation = true;
            return Valid;
        }
    }
}

/// <summary>Distinguishes exact context destruction from native clearing of the observed context epoch.</summary>
[Flags]
internal enum ApplicationDocumentEnd
{
    None = 0, IndividualDestroyed = 1, ContextsCleared = 2
}
