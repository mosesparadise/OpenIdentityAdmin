using System.Data.Common;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Witnesses actual committed transaction/context disposal and native response callbacks before injecting a publication failure.</summary>
internal sealed class ManagedHumanPublicationFault(ManagedHumanPublicationDefect defect) : DbTransactionInterceptor, IAsyncPageFilter
{
    private const string Failure = "Synthetic managed publication failure.";
    private const string Header = "X-Synthetic-Unplanned";
    private PlatformDbContext? database;
    internal Action? Advance
    {
        get; set;
    }
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
    internal int Faults
    {
        get; private set;
    }

    /// <summary>Preserves reverse DI disposal and real Razor callbacks without replacing any native auth or persistence dependency.</summary>
    internal void Configure(IServiceCollection services)
    {
        services.AddScoped<NativeClientCreationCleanup>();
        services.AddDbContext<PlatformDbContext>((provider, options) =>
        {
            _ = provider.GetRequiredService<NativeClientCreationCleanup>();
            options.AddInterceptors(this);
        });
        services.AddRazorPages().AddMvcOptions(options => options.Filters.Add(this));
    }

    /// <summary>Captures the exact real managed transaction at commit acknowledgement, after its actual durable write.</summary>
    public override Task TransactionCommittedAsync(DbTransaction transaction, TransactionEndEventData eventData, CancellationToken cancellationToken = default)
    {
        if (!Armed || eventData.Context is not PlatformDbContext current || !current.ChangeTracker.Entries<ManagedLoginInteraction>().Any())
        {
            return Task.CompletedTask;
        }
        Assert.Null(database);
        database = current;
        ProtocolStorageAmbient.Current!.Scope.Services.GetRequiredService<NativeClientCreationCleanup>().Complete = AfterDisposal;
        Commits++;
        if (defect == ManagedHumanPublicationDefect.Acknowledgement)
        {
            Faults++;
            throw new InvalidOperationException(Failure);
        }
        return Task.CompletedTask;
    }

    /// <summary>Requires actual context disposal before raising cleanup failure; output must remain unpublished.</summary>
    private void AfterDisposal()
    {
        Assert.IsType<ObjectDisposedException>(Record.Exception(() => _ = database!.Model));
        Disposals++;
        if (defect == ManagedHumanPublicationDefect.Expired)
        {
            Faults++;
            Advance!();
        }
        if (defect == ManagedHumanPublicationDefect.Disposal)
        {
            Faults++;
            throw new InvalidOperationException(Failure);
        }
    }

    /// <summary>Does not alter endpoint selection or native antiforgery.</summary>
    public Task OnPageHandlerSelectionAsync(PageHandlerSelectedContext context) => Task.CompletedTask;

    /// <summary>Adds one actual OnStarting mutation after the handler rendered, proving the frozen response rejects late callback changes.</summary>
    public async Task OnPageHandlerExecutionAsync(PageHandlerExecutingContext context, PageHandlerExecutionDelegate next)
    {
        if (Armed && defect == ManagedHumanPublicationDefect.Callback)
        {
            context.HttpContext.Response.OnStarting(() =>
            {
                Faults++;
                context.HttpContext.Response.Headers[Header] = bool.TrueString;
                return Task.CompletedTask;
            });
        }
        await next();
    }
}

/// <summary>Identifies separate real acknowledgement, cleanup and native callback publication boundaries.</summary>
public enum ManagedHumanPublicationDefect
{
    Acknowledgement, Disposal, Callback, Expired
}
