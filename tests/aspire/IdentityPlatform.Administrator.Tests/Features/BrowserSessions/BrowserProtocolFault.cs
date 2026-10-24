using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Injects one reached mutation into actual native prepared output or the final continuation database read.</summary>
internal sealed class BrowserProtocolFault : DbCommandInterceptor
{
    internal Fault? Selected
    {
        get; set;
    }
    internal Action? Advance
    {
        get; set;
    }
    internal DateTimeOffset? FreshnessDeadline
    {
        get; set;
    }
    internal int Reached
    {
        get; private set;
    }
    private OpenIddictServerEvents.ProcessSignInContext? active;
    private DbContext? database;
    private const int BeforeGeneration = 250;
    private const string ContinuationTable = "AdministratorIssuerContinuation";
    private const string Select = "SELECT";
    private const string ChangedOrigin = "different-native-request";
    private const string ChangedDigest = "0000000000000000000000000000000000000000000000000000000000000000";

    /// <summary>Observes the genuine prepared principals without replacing native validation, transactions or generators.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddDbContext<PlatformDbContext>((_, options) => options.AddInterceptors(this));
        services.AddOpenIddict().AddServer().AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.GenerateAccessToken.Descriptor.Order - BeforeGeneration).UseInlineHandler(async context =>
            {
                if (Selected is { } fault && context.EndpointType is OpenIddictServerEndpointType.Authorization or OpenIddictServerEndpointType.Token)
                {
                    active = context;
                    database = ProtocolStorageAmbient.Current!.Scope.Owner.Database;
                    if (fault == Fault.StoredProvenance)
                    {
                        await ChangeStoredAsync(context.CancellationToken);
                        Reached++;
                    }
                    else if (fault != Fault.FinalFreshness)
                    {
                        Change(context, fault);
                        Reached++;
                    }
                }
            }));
    }

    /// <summary>Changes a previously captured durable correlation under the actual issuance owner after initial admission and before final reread.</summary>
    private static ValueTask ChangeStoredAsync(CancellationToken token)
    {
        var scope = ProtocolStorageAmbient.Current!.Scope;
        return scope.Owner.RunAsync(async cancellation =>
        {
            var database = (PlatformDbContext)scope.Owner.Database;
            var row = await database.Set<IssuerContinuation>().SingleAsync(cancellation);
            row.CorrelationId = Guid.NewGuid();
            await database.SaveChangesAsync(cancellation);
        }, token, write: true);
    }

    /// <summary>Advances only after the exact final attached-row SELECT executes, independently of request start or earlier admission.</summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (Selected == Fault.FinalFreshness && Reached == 0 && active is { IsRequestHandled: true } &&
            ReferenceEquals(database, eventData.Context) && command.CommandText.TrimStart().StartsWith(Select, StringComparison.OrdinalIgnoreCase) &&
            command.CommandText.Contains(ContinuationTable, StringComparison.Ordinal))
        {
            Assert.NotNull(FreshnessDeadline);
            Assert.True(active.IdentityTokenPrincipal!.GetExpirationDate() > FreshnessDeadline);
            Assert.True(active.AccessTokenPrincipal!.GetExpirationDate() > FreshnessDeadline);
            Advance!();
            Reached++;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Changes exactly one private or public prepared binding after application admission but before native generation.</summary>
    private static void Change(OpenIddictServerEvents.ProcessSignInContext context, Fault fault)
    {
        switch (fault)
        {
            case Fault.ProjectedOrigin:
                context.Principal!.SetClaim(BrowserProtocolFlow.Origin, ChangedOrigin);
                break;
            case Fault.CodeOrigin:
                context.AuthorizationCodePrincipal!.SetClaim(BrowserProtocolFlow.Origin, ChangedOrigin);
                break;
            case Fault.IdentityIntent:
                context.IdentityTokenPrincipal!.SetClaim(BrowserProtocolFlow.Intent, ChangedDigest);
                break;
            case Fault.AccessLeak:
                context.AccessTokenPrincipal!.SetClaim(BrowserProtocolFlow.Expiry, 1L);
                break;
        }
    }

    /// <summary>Releases references after each faulted native operation and before fixture disposal.</summary>
    internal void Clear()
    {
        Selected = null;
        Advance = null;
        active = null;
        database = null;
    }

    internal enum Fault
    {
        ProjectedOrigin, CodeOrigin, IdentityIntent, AccessLeak, StoredProvenance, FinalFreshness
    }
}
