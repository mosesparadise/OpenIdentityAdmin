using System.Text.Json;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Persistence.Tests.Features.ProtocolStorage;

/// <summary>Exercises actual native HTTP extraction, private-key authentication, PAR generation and publication against the real provider.</summary>
internal static class NativeParIssuance
{
    private const string Path = "/par";
    private const string FormMediaType = "application/x-www-form-urlencoded";
    private const string Challenge = "0123456789abcdefghijklmnopqrstuvwxyzABCDEFG";
    private const string Nonce = "native-par-correlation";
    private const int MaximumLifetime = 60;
    private const int ResponseParameters = 2;
    private const string RequestedScopes = "openid identity.admin";

    /// <summary>Requires native protected persistence and then seeds failures which must withhold output and roll back issuance.</summary>
    internal static async Task VerifyAsync(IServiceProvider first, IServiceProvider second, NativeAssertionMaterial material,
        DateTimeOffset now, CancellationToken token)
    {
        var before = await NativeTokenBudgetFixture.CountAsync(second, token);
        var accepted = await InvokeAsync(first, material, now, token);
        Assert.True(accepted.Failure is null, accepted.Failure?.GetType().FullName + Environment.NewLine + accepted.Failure?.StackTrace);
        Assert.True(accepted.Claimed);
        Assert.Equal(StatusCodes.Status201Created, accepted.Status);
        using var json = JsonDocument.Parse(accepted.Body);
        Assert.Equal(ResponseParameters, json.RootElement.EnumerateObject().Count());
        var reference = json.RootElement.GetProperty(OpenIddictConstants.Parameters.RequestUri).GetString()!;
        var nativePrefix = reference.StartsWith(OpenIddictConstants.RequestUris.Prefixes.Generic, StringComparison.Ordinal);
        Assert.True(nativePrefix);
        Assert.InRange(json.RootElement.GetProperty(OpenIddictConstants.Parameters.ExpiresIn).GetInt64(), 1, MaximumLifetime);
        Assert.Equal((before.Tokens + 1, before.Authorizations), await NativeTokenBudgetFixture.CountAsync(second, token));
        await VerifyProtectedAsync(first, reference[OpenIddictConstants.RequestUris.Prefixes.Generic.Length..], token);
        await VerifyDenialsAsync(first, second, material, now, token);
    }

    /// <summary>Requires each injected defect to execute and preserves exact independent persisted counts after rejection.</summary>
    private static async Task VerifyDenialsAsync(IServiceProvider first, IServiceProvider second, NativeAssertionMaterial material,
        DateTimeOffset now, CancellationToken token)
    {
        var control = first.GetRequiredService<NativeParFault>();
        foreach (var fault in Enum.GetValues<NativeParFault.Kind>().Where(value => value != NativeParFault.Kind.None))
        {
            var before = await NativeTokenBudgetFixture.CountAsync(second, token);
            control.Select(fault);
            Result denied;
            try
            {
                denied = await InvokeAsync(first, material, now, token);
            }
            finally { control.Select(NativeParFault.Kind.None); }
            Assert.Equal(1, control.Observed);
            Assert.Equal(fault is NativeParFault.Kind.TokenMutation or NativeParFault.Kind.ApplicationMutation or NativeParFault.Kind.ScopeMutation ? 1 : 0, control.RowsChanged);
            Assert.False(denied.Claimed);
            Assert.Equal(StatusCodes.Status503ServiceUnavailable, denied.Status);
            Assert.True(denied.Body.Length == 0);
            var expected = fault is NativeParFault.Kind.UnknownCommit or NativeParFault.Kind.FinalRejected or NativeParFault.Kind.FinalSkipped ? (before.Tokens + 1, before.Authorizations) : before;
            Assert.Equal(expected, await NativeTokenBudgetFixture.CountAsync(second, token));
        }
    }

    /// <summary>Runs the real ProcessRequest context and requires the private post-cleanup permit before the HTTP boundary can publish success.</summary>
    private static async Task<Result> InvokeAsync(IServiceProvider root, NativeAssertionMaterial material, DateTimeOffset now, CancellationToken token)
    {
        using var form = new FormUrlEncodedContent(Fields(material, now));
        await using var body = await form.ReadAsStreamAsync(token);
        await using var output = new MemoryStream();
        var http = new DefaultHttpContext { RequestServices = root, RequestAborted = token };
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Host = new HostString(new Uri(NativeAssertionMaterial.Issuer).Host);
        http.Request.Path = Path;
        http.Request.Method = HttpMethods.Post;
        http.Request.ContentType = FormMediaType;
        http.Request.ContentLength = body.Length;
        http.Request.Body = body;
        var host = new NativeHttpCompletion();
        http.Features.Set<IHttpResponseFeature>(host);
        http.Response.Body = output;
        var claimed = false;
        Exception? failure = null;
        try
        {
            await ProtocolHttpBoundary.RunAsync(http, root.GetRequiredService<IServiceScopeFactory>(), root.GetRequiredService<TimeProvider>(), async (_, scope, cancellation) =>
            {
                try
                {
                    var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
                    transaction.BaseUri = new Uri(NativeAssertionMaterial.Issuer);
                    transaction.RequestUri = new Uri(transaction.BaseUri, Path);
                    transaction.Properties[typeof(HttpRequest).FullName!] = new WeakReference<HttpRequest>(http.Request);
                    var context = new OpenIddictServerEvents.ProcessRequestContext(transaction) { CancellationToken = cancellation };
                    await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
                    root.GetRequiredService<NativeParFault>().AfterDispatch(context);
                    AdministratorIssuanceCoordinator.ClaimPublication(http, context);
                    claimed = true;
                    return ProtocolResponseOutcome.Committed;
                }
                catch (Exception error) { failure = error; throw; }
            });
        }
        finally { await host.CompleteAsync(); }
        return new(http.Response.StatusCode, output.ToArray(), claimed, failure);
    }

    /// <summary>Supplies only the fixed confidential code/S256/PAR request through the native form extractor.</summary>
    private static Dictionary<string, string> Fields(NativeAssertionMaterial material, DateTimeOffset now) => new()
    {
        [OpenIddictConstants.Parameters.ClientId] = AdministratorProtocolIdentity.ClientId,
        [OpenIddictConstants.Parameters.ClientAssertionType] = OpenIddictConstants.ClientAssertionTypes.JwtBearer,
        [OpenIddictConstants.Parameters.ClientAssertion] = material.Sign(now.ToUnixTimeSeconds()),
        [OpenIddictConstants.Parameters.ResponseType] = OpenIddictConstants.ResponseTypes.Code,
        [OpenIddictConstants.Parameters.RedirectUri] = material.Policy.RedirectUri,
        [OpenIddictConstants.Parameters.Scope] = RequestedScopes,
        [OpenIddictConstants.Parameters.Resource] = AdministratorProtocolIdentity.Resource,
        [OpenIddictConstants.Parameters.CodeChallenge] = Challenge,
        [OpenIddictConstants.Parameters.CodeChallengeMethod] = OpenIddictConstants.CodeChallengeMethods.Sha256,
        [OpenIddictConstants.Parameters.Nonce] = Nonce,
        [OpenIddictConstants.Parameters.Prompt] = OpenIddictConstants.PromptValues.None
    };

    /// <summary>Validates the actual returned reference using installed native storage, decryption and signature validation in a fresh request.</summary>
    private static Task<bool> VerifyProtectedAsync(IServiceProvider root, string reference, CancellationToken token) =>
        new ProtocolStorageRequest(root.GetRequiredService<TimeProvider>(), token).RunAsync(root.GetRequiredService<IServiceScopeFactory>(), async (scope, cancellation) =>
        {
            var transaction = await scope.Services.GetRequiredService<IOpenIddictServerFactory>().CreateTransactionAsync();
            transaction.BaseUri = new Uri(NativeAssertionMaterial.Issuer);
            transaction.EndpointType = OpenIddictServerEndpointType.Authorization;
            transaction.Request = new()
            {
                ClientId = AdministratorProtocolIdentity.ClientId
            };
            var context = new OpenIddictServerEvents.ValidateTokenContext(transaction) { Token = reference, CancellationToken = cancellation };
            context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.Private.RequestToken);
            await scope.Services.GetRequiredService<IOpenIddictServerDispatcher>().DispatchAsync(context);
            Assert.False(context.IsRejected);
            Assert.False(context.IsRequestHandled);
            Assert.False(context.IsRequestSkipped);
            Assert.True(context.IsReferenceToken);
            Assert.False(string.IsNullOrEmpty(context.TokenId));
            Assert.NotNull(context.TokenValidationResult);
            Assert.True(context.TokenValidationResult.IsValid);
            Assert.NotNull(context.TokenValidationResult.SecurityToken);
            Assert.NotNull(context.Principal);
            Assert.Equal(OpenIddictConstants.RequestTokenTypes.Private.PushedAuthorizationRequest,
                context.Principal.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType));
            Assert.True(context.Principal.GetExpirationDate() > root.GetRequiredService<TimeProvider>().GetUtcNow());
            return true;
        });

    /// <summary>Retains bounded output and explicit completion observations so a catch-all boundary cannot hide failed assertions.</summary>
    private sealed record Result(int Status, byte[] Body, bool Claimed, Exception? Failure);
}
