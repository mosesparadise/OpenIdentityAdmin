using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Owns real request-phase scopes omitted by a disposable mutant so no unjoined native scope survives its witness.</summary>
internal sealed class PilotMutationScopes
{
    private readonly List<IAsyncDisposable> scopes = [];
    internal bool Armed
    {
        get; set;
    }

    /// <summary>Captures the actual DI scope supplied to scoped DbContext configuration, including validation and generation scopes.</summary>
    internal void Configure(IServiceCollection services) => services.AddDbContext<PlatformDbContext>((provider, _) =>
    {
        if (Armed)
        {
            lock (scopes)
            {
                var scope = Assert.IsAssignableFrom<IAsyncDisposable>(provider);
                if (!scopes.Contains(scope, ReferenceEqualityComparer.Instance))
                {
                    scopes.Add(scope);
                }
            }
        }
    });

    /// <summary>Disposes every captured scope; only the witness's exact deliberately injected cleanup failure may be absorbed.</summary>
    internal async Task DrainAsync(Exception expected)
    {
        Armed = false;
        var errors = new List<Exception>();
        foreach (var scope in scopes.AsEnumerable().Reverse())
        {
            try
            {
                await scope.DisposeAsync();
            }
            catch (Exception error)
            {
                if (!ReferenceEquals(error, expected))
                {
                    errors.Add(error);
                }
            }
        }
        if (errors.Count != 0)
        {
            throw new AggregateException(errors);
        }
    }
}
