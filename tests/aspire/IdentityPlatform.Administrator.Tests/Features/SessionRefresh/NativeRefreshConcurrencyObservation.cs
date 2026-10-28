using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using OpenIddict.EntityFrameworkCore.Models;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Observes the actual provider's stale-token concurrency failure without replacing or suppressing it.</summary>
internal sealed class NativeRefreshConcurrencyObservation : SaveChangesInterceptor
{
    internal string? ExpectedToken
    {
        get; set;
    }
    internal int Observed
    {
        get; private set;
    }

    /// <summary>Counts only the targeted native token conflict before EF throws its original concurrency exception.</summary>
    public override ValueTask<InterceptionResult> ThrowingConcurrencyExceptionAsync(ConcurrencyExceptionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (ExpectedToken is not null && eventData.Exception is DbUpdateConcurrencyException exception &&
            exception.Entries.Count == 1 && exception.Entries[0].Entity is OpenIddictEntityFrameworkCoreToken token && token.Id == ExpectedToken)
        {
            Observed++;
        }
        return ValueTask.FromResult(result);
    }
}
