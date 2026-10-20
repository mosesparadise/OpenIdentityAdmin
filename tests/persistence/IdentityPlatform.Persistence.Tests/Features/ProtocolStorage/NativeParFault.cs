using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.EntityFrameworkCore.Models;
using OpenIddict.Server;
using OpenIddict.Server.AspNetCore;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Seeds native pipeline defects after genuine validation/creation; positive execution uses unchanged installed handlers.</summary>
internal sealed class NativeParFault(NativeConsumptionCommit commit)
{
    private const int BeforeNativeHandler = 1;
    private const string CorruptedPayload = "deliberately-corrupted-native-payload";
    private const string EmptyJsonArray = "[]";
    private const string UnexpectedCredential = "forbidden-response-credential";
    private Kind selected;
    internal int Observed
    {
        get; private set;
    }
    internal int RowsChanged
    {
        get; private set;
    }

    /// <summary>Lists independently observable defects without replacing real native cryptography or persistence on the positive path.</summary>
    internal enum Kind
    {
        None, EarlyGenerationHandled, EarlyGenerationSkipped, TokenMutation, ApplicationMutation, ScopeMutation, ExtraResponse,
        UnboundedLifetime, EarlyFormatterHandled, UnknownCommit, FinalRejected, FinalSkipped
    }

    /// <summary>Resets observations only when arming a case, retaining them through unconditional teardown.</summary>
    internal void Select(Kind value)
    {
        selected = value;
        if (value != Kind.None)
        {
            Observed = 0;
            RowsChanged = 0;
        }
        else
        {
            commit.AfterCommit = null;
        }
    }

    /// <summary>Seeds a terminal outcome after native issuance returns but before the host claims its publication permit.</summary>
    internal void AfterDispatch(OpenIddictServerEvents.ProcessRequestContext context)
    {
        if (selected == Kind.FinalRejected)
        {
            Observed++;
            context.Reject(OpenIddictConstants.Errors.InvalidRequest);
        }
        if (selected == Kind.FinalSkipped)
        {
            Observed++;
            context.SkipRequest();
        }
    }

    /// <summary>Hooks actual native generation and formatting slots, while preserving all default filters and handler execution.</summary>
    internal static void Configure(OpenIddictServerBuilder builder)
    {
        builder.AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(handler => handler.UseInlineHandler(context =>
        {
            var fault = ProtocolStorageAmbient.Current!.Scope.Services.GetRequiredService<NativeParFault>();
            return fault.BeforeGenerationAsync(context);
        }).SetOrder(OpenIddictServerHandlers.Protection.CreateTokenEntry.Descriptor.Order - BeforeNativeHandler));
        builder.AddEventHandler<OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext>(handler => handler.UseInlineHandler(context =>
        {
            var fault = ProtocolStorageAmbient.Current!.Scope.Services.GetRequiredService<NativeParFault>();
            return fault.BeforeFormatAsync(context);
        }).SetOrder(OpenIddictServerAspNetCoreHandlers.ProcessJsonResponse<OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext>.Descriptor.Order - BeforeNativeHandler));
    }

    /// <summary>Proves that an early native handled/skipped result cannot stand in for actual generation, signing and storage.</summary>
    private ValueTask BeforeGenerationAsync(OpenIddictServerEvents.GenerateTokenContext context)
    {
        if (selected == Kind.EarlyGenerationHandled)
        {
            Observed++;
            context.HandleRequest();
        }
        if (selected == Kind.EarlyGenerationSkipped)
        {
            Observed++;
            context.SkipRequest();
        }
        return ValueTask.CompletedTask;
    }

    /// <summary>Mutates actual persisted state after native generation, or seeds response/commit defects before actual formatting.</summary>
    private async ValueTask BeforeFormatAsync(OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext context)
    {
        if (selected is Kind.None or Kind.EarlyGenerationHandled or Kind.EarlyGenerationSkipped or Kind.FinalRejected or Kind.FinalSkipped || !string.IsNullOrEmpty(context.Response.Error))
        {
            return;
        }
        Observed++;
        if (selected == Kind.ExtraResponse)
        {
            context.Response.AccessToken = UnexpectedCredential;
        }
        if (selected == Kind.UnboundedLifetime)
        {
            context.Response.ExpiresIn = long.MaxValue;
        }
        if (selected == Kind.EarlyFormatterHandled)
        {
            context.HandleRequest();
        }
        if (selected == Kind.UnknownCommit)
        {
            commit.AfterCommit = () => throw new ArithmeticException();
        }
        if (selected is Kind.TokenMutation or Kind.ApplicationMutation or Kind.ScopeMutation)
        {
            var scope = ProtocolStorageAmbient.Current!.Scope;
            RowsChanged = await MutateAsync(scope, context, context.CancellationToken);
        }
    }

    /// <summary>Uses bulk database updates to leave tracked native snapshots unchanged; row count and profile revision remain unchanged.</summary>
    private async ValueTask<int> MutateAsync(ProtocolStorageScope scope, OpenIddictServerEvents.ApplyPushedAuthorizationResponseContext context, CancellationToken token)
    {
        if (selected == Kind.ApplicationMutation)
        {
            return await scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreApplication>()
                .Where(row => row.ClientId == AdministratorProtocolIdentity.ClientId).ExecuteUpdateAsync(update => update.SetProperty(row => row.Permissions, EmptyJsonArray), current)), token, write: true);
        }
        if (selected == Kind.ScopeMutation)
        {
            return await scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreScope>()
                .Where(row => row.Name == AdministratorProtocolIdentity.Scope).ExecuteUpdateAsync(update => update.SetProperty(row => row.Resources, EmptyJsonArray), current)), token, write: true);
        }
        var reference = context.Response.RequestUri![OpenIddictConstants.RequestUris.Prefixes.Generic.Length..];
        var manager = scope.Services.GetRequiredService<IOpenIddictTokenManager>();
        var row = await manager.FindByReferenceIdAsync(reference, token);
        var identifier = await manager.GetIdAsync(row!, token);
        return await scope.Owner.RunAsync(current => new ValueTask<int>(scope.Owner.Database.Set<OpenIddictEntityFrameworkCoreToken>().Where(row => row.Id == identifier)
            .ExecuteUpdateAsync(update => update.SetProperty(row => row.Payload, CorruptedPayload), current)), token, write: true);
    }
}
