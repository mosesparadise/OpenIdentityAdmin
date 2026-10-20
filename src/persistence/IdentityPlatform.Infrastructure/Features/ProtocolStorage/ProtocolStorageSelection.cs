namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Freezes explicit per-scope native-store selection before a manager can capture its store.</summary>
internal sealed class ProtocolStorageSelection(ProtocolStorageOwner owner)
{
    private readonly object sync = new();
    private ProtocolStorageMode mode;
    private bool resolved;
    private bool failed;

    /// <summary>Allows one declared mode only; an absent, repeated or late selection cannot recover a failed scope.</summary>
    internal void Select(ProtocolStorageMode value)
    {
        lock (sync)
        {
            if (failed || resolved || mode != ProtocolStorageMode.Unselected || value is not (ProtocolStorageMode.Owned or ProtocolStorageMode.LegacyPilot))
            {
                Fail();
            }
            mode = value;
        }
    }

    /// <summary>Freezes the already selected mode before constructing any native manager or store.</summary>
    internal bool ResolveOwned()
    {
        lock (sync)
        {
            if (failed || mode == ProtocolStorageMode.Unselected)
            {
                Fail();
            }
            resolved = true;
            return mode == ProtocolStorageMode.Owned;
        }
    }

    /// <summary>Rejects an unexpected resolved profile even when native extraction and validation occur in different events.</summary>
    internal void Require(ProtocolStorageMode expected)
    {
        lock (sync)
        {
            if (failed || mode != expected || mode == ProtocolStorageMode.Unselected)
            {
                Fail();
            }
        }
    }

    /// <summary>Latches failure in both selection and the request owner so a caught resolution error cannot recover acceptance.</summary>
    private void Fail()
    {
        failed = true;
        owner.Fail();
        throw ProtocolStorageOwner.Unavailable();
    }
}

internal enum ProtocolStorageMode
{
    Unselected, Owned, LegacyPilot
}
