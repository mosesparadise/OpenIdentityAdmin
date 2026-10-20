using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Combines the existing exact-row pre-update redemption barrier with actual peer profile-lock session observation.</summary>
internal sealed class NativeParRaceProbe : DbCommandInterceptor
{
    private const string Update = "UPDATE";
    internal NativeRedemptionBarrier Redemption { get; } = new();
    internal TaskCompletionSource<int>? BeforeLock
    {
        get; set;
    }

    /// <summary>Installs observers on this issuer's actual Platform contexts without replacing commands or manager handlers.</summary>
    internal void Configure(IServiceCollection services) =>
        services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this, Redemption));

    /// <summary>Records the exact peer session immediately before its real profile UPDATE can block behind native redemption.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData data,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (BeforeLock is { } signal && data.Context?.Database.CurrentTransaction is not null &&
            data.Context.Model.FindEntityType(typeof(AdministratorProtocolProfile))?.GetTableName() is { } table &&
            command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(table, StringComparison.Ordinal))
        {
            signal.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }
}
