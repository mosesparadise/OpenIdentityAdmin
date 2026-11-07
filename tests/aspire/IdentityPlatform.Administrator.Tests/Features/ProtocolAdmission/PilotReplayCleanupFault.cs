using System.Data.Common;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Fails only an explicitly armed native token update to verify rejected replay remains rejected when chain cleanup cannot be persisted.</summary>
internal sealed class PilotReplayCleanupFault : DbCommandInterceptor
{
    private const string Update = "UPDATE";
    private const string Tokens = "OpenIddictTokens";
    private const string Failure = "Synthetic native replay cleanup failure.";
    internal bool Armed
    {
        get; set;
    }
    internal int Failures
    {
        get; private set;
    }

    /// <summary>Uses the actual issuer database pipeline without replacing native managers or provider constraints.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));

    /// <summary>Injects a finite native-write failure only after the test explicitly seeds a revoked code with valid linked access.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && command.CommandText.TrimStart().StartsWith(Update, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(Tokens, StringComparison.Ordinal))
        {
            Failures++;
            throw new InvalidOperationException(Failure);
        }
        return ValueTask.FromResult(result);
    }
}
