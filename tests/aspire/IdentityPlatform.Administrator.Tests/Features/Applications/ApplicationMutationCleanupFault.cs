using System.Data.Common;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Fails actual child cleanup only after a confirmed native commit, independently proving output withholding without a production seam.</summary>
internal sealed class ApplicationMutationCleanupFault : DbTransactionInterceptor
{
    private const string Message = "Synthetic application child cleanup failure.";
    internal Exception Failure { get; } = new InvalidOperationException(Message);
    internal bool Armed
    {
        get; set;
    }
    internal int Commits
    {
        get; private set;
    }
    internal int Disposals
    {
        get; private set;
    }

    /// <summary>Constructs the scoped sentinel before its native context so reverse disposal can prove that context is already gone.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<NativeClientCreationCleanup>();
        services.AddDbContext<PlatformDbContext>((provider, options) =>
        {
            _ = provider.GetRequiredService<NativeClientCreationCleanup>();
            options.AddInterceptors(this);
        });
    }

    /// <summary>Arms only the committed mutation's child, leaving setup and observation scopes unaffected.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (Armed && eventData.Context!.ChangeTracker.Entries<ApplicationPolicy>().Any())
        {
            Commits++;
            var database = eventData.Context;
            ProtocolStorageAmbient.Current!.Scope.Services.GetRequiredService<NativeClientCreationCleanup>().Complete = () => AfterDisposal(database);
        }
        return Task.CompletedTask;
    }

    /// <summary>Confirms actual context disposal and one commit before throwing the exact retained failure.</summary>
    private void AfterDisposal(DbContext database)
    {
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = database.Model));
        Assert.Equal(1, Commits);
        Disposals++;
        throw Failure;
    }

    /// <summary>Rejects unrelated exceptions rather than accepting any failure as evidence for the injected cleanup fault.</summary>
    internal bool Contains(Exception? error) => ReferenceEquals(error, Failure) ||
        (error is AggregateException aggregate ? aggregate.InnerExceptions.Count != 0 && aggregate.InnerExceptions.All(Contains) : error?.InnerException is { } inner && Contains(inner));
}
