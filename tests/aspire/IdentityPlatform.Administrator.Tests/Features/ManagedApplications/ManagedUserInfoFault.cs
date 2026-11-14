using System.Data.Common;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Injects faults at actual UserInfo formatter, provider-acknowledgement and disposed-context boundaries without replacing their implementations.</summary>
internal sealed class ManagedUserInfoFault(UserInfoPublicationFailure failure) : DbTransactionInterceptor
{
    private const string UnapprovedHeader = "X-Unapproved-Profile";
    private const string InjectedValue = "synthetic";

    private const string Message = "Synthetic UserInfo publication failure.";
    private HttpContext? http;
    private PlatformDbContext? database;
    private NativeClientCreationCleanup? cleanup;
    internal Action? Advance
    {
        get; set;
    }
    internal bool Armed
    {
        get; set;
    }
    internal int Faults
    {
        get; private set;
    }
    internal int Commits
    {
        get; private set;
    }
    internal int Disposals
    {
        get; private set;
    }

    /// <summary>Preserves actual native handoff and reverse disposal while recording only the selected projection transaction.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<NativeClientCreationCleanup>();
        services.AddDbContext<PlatformDbContext>((provider, options) =>
        {
            var sentinel = provider.GetRequiredService<NativeClientCreationCleanup>();
            if (http is not null)
            {
                cleanup = sentinel;
            }
            options.AddInterceptors(this);
        });
        services.AddOpenIddict().AddServer(server => server.AddEventHandler<OpenIddictServerEvents.HandleUserInfoRequestContext>(handler => handler
            .SetOrder(OpenIddictServerAspNetCoreHandlers.EnablePassthroughMode<OpenIddictServerEvents.HandleUserInfoRequestContext,
                OpenIddictServerAspNetCoreHandlerFilters.RequireUserInfoEndpointPassthroughEnabled>.Descriptor.Order - 1)
            .UseInlineHandler(context =>
            {
                if (Armed)
                {
                    http = context.Transaction.GetHttpRequest()!.HttpContext;
                }
                return ValueTask.CompletedTask;
            })));
    }

    /// <summary>Registers an actual child response callback only after the real projection transaction begins.</summary>
    public override ValueTask<DbTransaction> TransactionStartedAsync(DbConnection connection, TransactionEndEventData eventData, DbTransaction result,
        CancellationToken cancellationToken = default)
    {
        if (Armed && http is not null)
        {
            Assert.Null(database);
            database = Assert.IsType<PlatformDbContext>(eventData.Context);
            Assert.NotNull(cleanup);
            cleanup.Complete = Disposed;
            if (failure == UserInfoPublicationFailure.Callback)
            {
                http.Response.OnStarting(() =>
                {
                    http.Response.Headers[UnapprovedHeader] = InjectedValue;
                    Faults++;
                    return Task.CompletedTask;
                });
            }
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Fails acknowledgement only after actual commit; authority stays unchanged with exactly the portable lock-counter increments.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (ReferenceEquals(database, eventData.Context))
        {
            Commits++;
            if (failure == UserInfoPublicationFailure.Commit)
            {
                Faults++;
                throw new InvalidOperationException(Message);
            }
        }
        return Task.CompletedTask;
    }

    /// <summary>Proves real context disposal before failure or original publication-deadline expiry is introduced.</summary>
    private void Disposed()
    {
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = database!.Model));
        Disposals++;
        if (failure == UserInfoPublicationFailure.Cleanup)
        {
            Faults++;
            throw new InvalidOperationException(Message);
        }
        if (failure == UserInfoPublicationFailure.Expired)
        {
            Faults++;
            Advance!();
        }
    }
}

/// <summary>Names independently injected late publication failures rather than inferred handled flags.</summary>
public enum UserInfoPublicationFailure
{
    Callback, Commit, Cleanup, Expired
}
