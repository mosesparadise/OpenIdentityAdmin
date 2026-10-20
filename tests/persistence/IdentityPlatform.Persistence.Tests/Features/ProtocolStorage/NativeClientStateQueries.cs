using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Persistence.Tests.Features.ProviderCompatibility;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Observes real coordination commands and injects same-count row drift during the late quota query in this disposable fixture.</summary>
internal sealed class NativeClientStateQueries(NativeClientStateFault fault) : DbCommandInterceptor
{
    private const string Count = "COUNT";
    private const string Tokens = "OpenIddictTokens";
    private const string UpdatePayload = "UPDATE \"bff\".\"OpenIddictTokens\" SET \"Payload\" = @changed WHERE \"Id\" = @identifier";
    private const string UpdateReference = "UPDATE \"bff\".\"OpenIddictTokens\" SET \"ReferenceId\" = @changed WHERE \"Id\" = @identifier";
    private const string ChangedParameter = "changed";
    private const string IdentifierParameter = "identifier";
    private const string Changed = "synthetic-late-row-change";
    internal TaskCompletionSource<int>? BeforeLock
    {
        get; set;
    }
    internal TaskCompletionSource<int>? Locked
    {
        get; set;
    }
    internal Task? Release
    {
        get; set;
    }

    /// <summary>Captures the exact peer connection before its real coordination update blocks.</summary>
    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (IsCoordination(command, eventData))
        {
            BeforeLock?.TrySetResult(NativeLockObservation.Session(command.Connection!));
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Pauses the winner only after the provider really granted its coordination lock.</summary>
    public override async ValueTask<int> NonQueryExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        int result, CancellationToken cancellationToken = default)
    {
        if (IsCoordination(command, eventData) && Locked is { } locked)
        {
            Locked = null;
            locked.TrySetResult(NativeLockObservation.Session(command.Connection!));
            await (Release ?? Task.CompletedTask).WaitAsync(cancellationToken);
        }
        return result;
    }

    /// <summary>Changes the actual row before the post-generation count without changing total capacity or the EF tracked entity.</summary>
    public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
        InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    {
        if (fault.PendingIdentifier is { } identifier && command.CommandText.Contains(Count, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(Tokens, StringComparison.Ordinal))
        {
            fault.PendingIdentifier = null;
            fault.Calls++;
            await using var mutation = command.Connection!.CreateCommand();
            mutation.Transaction = command.Transaction;
            mutation.CommandText = fault.Mode == NativeClientStateFault.Kind.ChangedLateReference ? UpdateReference : UpdatePayload;
            Add(mutation, ChangedParameter, Changed);
            Add(mutation, IdentifierParameter, identifier);
            Assert.Equal(1, await mutation.ExecuteNonQueryAsync(cancellationToken));
        }
        return result;
    }

    /// <summary>Uses the configured EF table mapping to observe only this feature's native coordination mutation.</summary>
    private static bool IsCoordination(DbCommand command, CommandEventData data) =>
        data.Context?.Model.FindEntityType(typeof(AdministratorClientStateBudget))?.GetTableName() is { } table &&
        command.CommandText.Contains(table, StringComparison.Ordinal);

    /// <summary>Uses ordinary bound parameters for every synthetic value in the injected persistence fault.</summary>
    private static void Add(DbCommand command, string name, string value)
    {
        var parameter = command.CreateParameter();
        parameter.ParameterName = name;
        parameter.Value = value;
        command.Parameters.Add(parameter);
    }
}
