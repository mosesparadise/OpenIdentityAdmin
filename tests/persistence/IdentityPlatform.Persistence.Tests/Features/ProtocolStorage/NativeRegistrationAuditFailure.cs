using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Fails the actual registration audit save after native application and scope saves have completed.</summary>
internal sealed class NativeRegistrationAuditFailure : SaveChangesInterceptor
{
    internal Action? BeforeAuditSave
    {
        get; set;
    }

    /// <summary>Consumes the fault only when the owned context attempts to persist its staged security audit.</summary>
    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context!.ChangeTracker.Entries<SecurityAuditIntent>().Any(entry => entry.State == EntityState.Added))
        {
            var action = BeforeAuditSave;
            BeforeAuditSave = null;
            action?.Invoke();
        }
        return ValueTask.FromResult(result);
    }
}
