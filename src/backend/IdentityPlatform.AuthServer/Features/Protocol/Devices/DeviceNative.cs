using System.Runtime.CompilerServices;
using System.Security.Claims;
using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.AuthServer.Features.Protocol.Devices;

/// <summary>Owns one device protocol transaction inside the original request scope, including native validation, generation and error formatting.</summary>
internal sealed class DeviceNative
{
    internal const string AuthorizationPath = "/connect/device";
    internal const string VerificationPath = "/connect/verify";
    private static readonly ConditionalWeakTable<OpenIddictServerTransaction, DeviceNative> Sessions = new();
    private KeyValuePair<string, OpenIddictParameter>[]? input;
    private readonly ProtocolStorageScope scope;
    private readonly IOpenIddictServerDispatcher dispatcher;
    internal OpenIddictServerTransaction Transaction
    {
        get;
    }

    /// <summary>Binds the native dispatcher and its original storage scope for one device request.</summary>
    private DeviceNative(ProtocolStorageScope scope, OpenIddictServerTransaction transaction)
    {
        this.scope = scope;
        Transaction = transaction;
        dispatcher = scope.Services.GetRequiredService<IOpenIddictServerDispatcher>();
    }

    /// <summary>Creates an issuer-owned transaction with only the request allowed by the device or human form boundary.</summary>
    internal static async ValueTask<DeviceNative> CreateAsync(ProtocolStorageScope scope, OpenIddictServerEndpointType endpoint, OpenIddictRequest request, HttpContext? http = null)
    {
        var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
        transaction.BaseUri = transaction.Options.Issuer ?? throw ProtocolStorageOwner.Unavailable();
        transaction.EndpointType = endpoint;
        var path = endpoint switch
        {
            OpenIddictServerEndpointType.DeviceAuthorization => AuthorizationPath,
            OpenIddictServerEndpointType.EndUserVerification => VerificationPath,
            OpenIddictServerEndpointType.Token when request.IsDeviceCodeGrantType() => ProtocolOptions.TokenPath,
            _ => throw ProtocolStorageOwner.Unavailable()
        };
        transaction.RequestUri = new(transaction.BaseUri, path);
        transaction.Request = request;
        transaction.Response = new OpenIddictResponse();
        if (http is not null)
        {
            transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(http.Request);
        }
        var session = new DeviceNative(scope, transaction);
        Sessions.Add(transaction, session);
        return session;
    }

    /// <summary>Freezes the original backchannel parameters before any native validator can run.</summary>
    internal void BindBackchannel() => input = Transaction.Request!.GetParameters().ToArray();

    /// <summary>Requires every originally admitted form parameter to survive native processing unchanged.</summary>
    internal void RequireInput()
    {
        if (input is not null && (Transaction.Request is not { } request || input.Length != request.GetParameters().Count ||
            input.Any(pair => request.GetParameter(pair.Key) != pair.Value)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Recognizes only the exact privately created transaction within its still-active storage scope.</summary>
    internal static bool Owns(OpenIddictServerTransaction transaction) => Sessions.TryGetValue(transaction, out var device) &&
        ReferenceEquals(device.scope, ProtocolStorageAmbient.Current?.Scope);

    /// <summary>Resolves the actual native human code and saves the authentication result for the library's one-use decision handlers.</summary>
    internal async ValueTask<ClaimsPrincipal?> UserAsync(CancellationToken token)
    {
        var auth = new OpenIddictServerEvents.ProcessAuthenticationContext(Transaction) { CancellationToken = token };
        await dispatcher.DispatchAsync(auth);
        Transaction.SetProperty(typeof(OpenIddictServerEvents.ProcessAuthenticationContext).FullName!, auth);
        return !auth.IsRejected && !auth.IsRequestHandled && !auth.IsRequestSkipped ? auth.UserCodePrincipal : null;
    }

    /// <summary>Issues or updates the native code or token with its original validation state and formatter.</summary>
    internal async ValueTask<OpenIddictServerEvents.ProcessSignInContext> SignInAsync(ClaimsPrincipal principal, CancellationToken token)
    {
        var signIn = new OpenIddictServerEvents.ProcessSignInContext(Transaction) { CancellationToken = token, Principal = principal };
        await dispatcher.DispatchAsync(signIn);
        if (!signIn.IsRequestHandled || signIn.IsRejected || signIn.IsRequestSkipped)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return signIn;
    }

    /// <summary>Denies the authenticated user-code transaction using the native paired-entry challenge handlers.</summary>
    internal async ValueTask DenyAsync(CancellationToken token)
    {
        var denial = new OpenIddictServerEvents.ProcessChallengeContext(Transaction) { CancellationToken = token };
        await dispatcher.DispatchAsync(denial);
        if (!denial.IsRequestHandled || denial.IsRejected || denial.IsRequestSkipped)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }

    /// <summary>Formats only the private verification transaction as an internal result for the account page.</summary>
    internal static void Configure(OpenIddictServerBuilder server)
    {
        server.AddEventHandler<OpenIddictServerEvents.ApplyDeviceAuthorizationResponseContext>(handler => handler.UseInlineHandler(context =>
        {
            if (Owns(context.Transaction))
            {
                context.Response.VerificationUriComplete = null;
            }
            return ValueTask.CompletedTask;
        }).SetOrder(int.MinValue + 1));
        server.AddEventHandler<OpenIddictServerEvents.ApplyEndUserVerificationResponseContext>(handler => handler.UseInlineHandler(context =>
        {
            if (Owns(context.Transaction))
            {
                context.HandleRequest();
            }
            return ValueTask.CompletedTask;
        }).SetOrder(int.MinValue + 1));
    }
}
