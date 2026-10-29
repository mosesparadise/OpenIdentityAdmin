using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Freezes explicit per-scope native-store selection before a manager can capture its store.</summary>
internal sealed class ProtocolStorageSelection(ProtocolStorageOwner owner)
{
    private readonly object sync = new();
    private ProtocolStorageMode mode;
    private bool resolved;
    private bool failed;
    private Action? apiReadGuard;

    /// <summary>Allows one declared mode only; an absent, repeated or late selection cannot recover a failed scope.</summary>
    internal void Select(ProtocolStorageMode value)
    {
        if (value is not (ProtocolStorageMode.Owned or ProtocolStorageMode.LegacyPilot))
        {
            Fail();
        }
        SelectCore(value);
    }

    /// <summary>Selects ordinary native stores only after an exact rollback-only API context has acquired its declared locks.</summary>
    internal async ValueTask SelectApiReadAsync(CeremonyTransaction transaction, PlatformDbContext database, string userId)
    {
        try
        {
            transaction.RequireApiRead(database, userId);
            await owner.RequireUnactivatedContextAsync(database, transaction.Token);
            transaction.RequireApiRead(database, userId);
            apiReadGuard = () => { owner.RequireNeverActivated(); transaction.RequireApiRead(database, userId); };
            SelectCore(ProtocolStorageMode.ApiRead);
        }
        catch { Fail(); throw; }
    }

    /// <summary>Freezes one explicitly checked mode before any manager can resolve a store.</summary>
    private void SelectCore(ProtocolStorageMode value)
    {
        lock (sync)
        {
            if (failed || resolved || mode != ProtocolStorageMode.Unselected)
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
            if (mode == ProtocolStorageMode.ApiRead)
            {
                try
                {
                    (apiReadGuard ?? throw ProtocolStorageOwner.Unavailable())();
                }
                catch { Fail(); throw; }
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
    Unselected, Owned, LegacyPilot, ApiRead
}
