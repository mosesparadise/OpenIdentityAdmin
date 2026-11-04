using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Witnesses actual changed SQL rows before either a synthetic post-save failure or a real audit permission denial.</summary>
internal sealed class ApplicationUpdateFault(bool throwAfterSave) : SaveChangesInterceptor
{
    private const string Message = "Synthetic failure after persisted native update.";
    internal InvalidOperationException Failure { get; } = new(Message);
    internal string? ApplicationId
    {
        get; set;
    }
    internal ApplicationUpdateSnapshot? Persisted
    {
        get; private set;
    }
    internal bool Armed
    {
        get; set;
    }

    /// <summary>Reads fresh untracked database values after native SaveChanges; the surrounding transaction has not committed.</summary>
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        if (Armed && Persisted is null)
        {
            Persisted = await ApplicationUpdateSnapshot.ReadAsync((PlatformDbContext)eventData.Context!, ApplicationId!, cancellationToken);
            if (throwAfterSave)
            {
                throw Failure;
            }
        }
        return result;
    }
}
