using System.Data.Common;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Observes the exact native consumption child's successful provider commit and independently reads its original input afterward.</summary>
internal sealed class ManagedRedemptionWitness : DbTransactionInterceptor
{
    private const string Valid = "valid";
    private const string Missing = "The exact native consumption commit was not observed.";
    private const string EarlyFailure = "Synthetic failure before native redemption.";
    private bool entered;
    private DbTransaction? transaction;
    private Guid transactionId;
    private object? database;
    internal string? Identifier { get; set; }
    internal Func<Task<string?>>? ReadStatus { get; set; }
    internal bool FailBeforeRedemption { get; set; }
    internal bool EarlyFailed { get; private set; }
    internal bool Observed { get; private set; }
    internal bool InputRemainedValid { get; private set; }

    /// <summary>Adds passive actual-commit observation and a separately armed early-failure control without replacing native handlers or stores.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));
        services.AddOpenIddict().AddServer(server => server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor.Order - 1).UseInlineHandler(BeforeRedemptionAsync)));
    }

    /// <summary>Arms only at the original input's real redemption slot; earlier validation commits and unrelated grants cannot satisfy the witness.</summary>
    private ValueTask BeforeRedemptionAsync(OpenIddictServerEvents.ProcessSignInContext context)
    {
        if (Identifier is not null && ManagedConsumption.Original(context)?.GetTokenId() == Identifier)
        {
            if (FailBeforeRedemption)
            {
                EarlyFailed = true;
                throw new InvalidOperationException(EarlyFailure);
            }
            entered = true;
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Binds the first actual owned child commit attempt after the exact native redemption slot; an attempt alone is not acceptance.</summary>
    public override ValueTask<InterceptionResult> TransactionCommittingAsync(DbTransaction transaction, TransactionEventData eventData,
        InterceptionResult result, CancellationToken cancellationToken = default)
    {
        if (entered && this.transaction is null && eventData.Context is not null &&
            ReferenceEquals(eventData.Context, ProtocolStorageAmbient.Current?.Scope?.Owner.Database))
        {
            this.transaction = transaction;
            transactionId = eventData.TransactionId;
            database = eventData.Context;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Observes only that child's successful provider commit, then reads the exact input through a separate context/connection.</summary>
    public override async Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(this.transaction, transaction) && transactionId == eventData.TransactionId && ReferenceEquals(database, eventData.Context))
        {
            InputRemainedValid = await ReadStatus!() == Valid;
            Observed = true;
        }
    }

    /// <summary>Rejects unrelated early outages and failed commits before the behavioral mutation marker can be evaluated.</summary>
    internal void RequireObserved()
    {
        if (!Observed)
        {
            throw new InvalidOperationException(Missing);
        }
    }
}
