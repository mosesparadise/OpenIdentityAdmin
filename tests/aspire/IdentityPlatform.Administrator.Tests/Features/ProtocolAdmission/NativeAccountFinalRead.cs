using System.Data.Common;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Changes the native continuation only after the final persisted session read has actually executed on the selected provider.</summary>
internal sealed class NativeAccountFinalRead : DbCommandInterceptor
{
    private const string SessionTable = "AdministratorSession";
    private const string SelectVerb = "SELECT";
    private const string Changed = "changed-final-binding";
    private const int AfterAdmission = 750;
    private OpenIddictServerEvents.ProcessSignInContext? signIn;
    private DbContext? database;
    internal OpenIddictServerEndpointType Endpoint
    {
        get; set;
    }
    internal Change? Selected
    {
        get; set;
    }
    internal int Observed
    {
        get; private set;
    }

    /// <summary>Records the real admitted sign-in and owning database without replacing its native principal or production handlers.</summary>
    internal static void Configure(OpenIddictServerBuilder server)
    {
        server.Services.AddSingleton<NativeAccountFinalRead>();
        server.Services.AddDbContext<PlatformDbContext>((services, options) => options.AddInterceptors(services.GetRequiredService<NativeAccountFinalRead>()));
        server.AddEventHandler<OpenIddictServerEvents.ProcessSignInContext>(handler => handler
            .SetOrder(OpenIddictServerHandlers.RestoreInternalClaims.Descriptor.Order + AfterAdmission).UseInlineHandler(context =>
            {
                var control = context.Transaction.GetHttpRequest()!.HttpContext.RequestServices.GetRequiredService<NativeAccountFinalRead>();
                if (control.Selected is not null && context.EndpointType == control.Endpoint)
                {
                    control.signIn = context;
                    control.database = ProtocolStorageAmbient.Current!.Scope.Owner.Database;
                }
                return ValueTask.CompletedTask;
            }));
    }

    /// <summary>Requires native formatting to have completed and the exact issuance context to be performing its final authority reload.</summary>
    public override ValueTask<DbDataReader> ReaderExecutedAsync(DbCommand command, CommandExecutedEventData eventData,
        DbDataReader result, CancellationToken cancellationToken = default)
    {
        if (Selected is { } change && signIn is { IsRequestHandled: true } current && Observed == 0 &&
            ReferenceEquals(database, eventData.Context) && ReferenceEquals(database, ProtocolStorageAmbient.Current?.Scope.Owner.Database) &&
            command.CommandText.TrimStart().StartsWith(SelectVerb, StringComparison.OrdinalIgnoreCase) && command.CommandText.Contains(SessionTable, StringComparison.Ordinal))
        {
            switch (change)
            {
                case Change.PublicSubject:
                    current.Principal!.SetClaim(OpenIddictConstants.Claims.Subject, Changed);
                    break;
                case Change.PrivateResource:
                    current.Principal!.SetResources(Changed);
                    break;
                case Change.NativeRequest:
                    current.Request.State = Changed;
                    break;
                case Change.PrincipalClone:
                    current.Principal = current.Principal!.Clone(static _ => true);
                    break;
            }
            Observed++;
        }
        return ValueTask.FromResult(result);
    }

    /// <summary>Releases references to the completed native request and its scoped context after the actual operation drains.</summary>
    internal void Clear()
    {
        Selected = null;
        signIn = null;
        database = null;
    }

    internal enum Change
    {
        PublicSubject, PrivateResource, NativeRequest, PrincipalClone
    }
}
