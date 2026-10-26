using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Antiforgery;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Connects native protected browser forms to atomic local invalidation and bounded explicit acknowledgement recovery.</summary>
internal sealed class BrowserLogoutHandler(AdminSessionDbContext database, TimeProvider clock, BrowserLoginProof proofs,
    BrowserLoginService login, SessionTicketCodec codec, BrowserSessionDestinations destinations, IAntiforgery antiforgery)
{
    /// <summary>Rejects ambiguous framing before durable work and publishes no successful mutation until its outer owned scope has disposed.</summary>
    internal async Task<IResult> RunAsync(HttpContext http)
    {
        var post = HttpMethods.IsPost(http.Request.Method);
        if (post ? !await BrowserLoginRequest.FormAsync(http, destinations, BrowserLogoutRequest.Choice) :
            !BrowserLoginRequest.Get(http.Request) || http.Request.Query.Count != 0 || http.Request.ContentType is not null || !await BrowserLoginRequest.EmptyAsync(http))
        {
            return Results.BadRequest();
        }
        var choice = post ? http.Request.Form[BrowserLogoutRequest.Choice].ToString() : string.Empty;
        if (choice is not ("" or BrowserLogoutRequest.Local or BrowserLogoutRequest.Issuer))
        {
            return Results.BadRequest();
        }
        var admission = await ReadAsync(http);
        if (admission is null || !BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Session, out var handle))
        {
            return Results.BadRequest();
        }
        var (proof, reconciliation) = admission.Value;
        var actor = await new BrowserSessionActor(database, clock, codec).ReadAsync(proof, handle, http.RequestAborted);
        if (!actor.Allowed)
        {
            return Results.BadRequest();
        }
        if (!post)
        {
            return BrowserLogoutPage.Form(http, antiforgery.GetAndStoreTokens(http).RequestToken ?? throw new InvalidOperationException(BrowserLoginBoundary.Unavailable));
        }
        await antiforgery.ValidateRequestAsync(http);
        return await CommitAsync(http, proof, reconciliation, actor.Digest, choice);
    }

    /// <summary>Reserves optional remote work only from authenticated provenance in the same atomic local invalidation.</summary>
    private async Task<IResult> CommitAsync(HttpContext http, BrowserGenerationProof proof, bool reconciliation, string? actor, string choice)
    {
        var remote = choice == BrowserLogoutRequest.Issuer && !reconciliation && actor is not null;
        var state = remote ? BrowserLoginSelector.CreateSelector() : null;
        var result = reconciliation
            ? await new BrowserLogoutReconciliation(database, clock).ReconcileAsync(proof.Digest, proof.Generation, http.RequestAborted)
            : await new BrowserLogoutService(database, clock, codec).LogoutAsync(proof.Digest, proof.Generation, actor, state is null ? null : BrowserLoginSelector.Digest(state), http.RequestAborted);
        if (result is null)
        {
            return Results.Conflict();
        }
        Publish(http, proof, result);
        if (result.Remote is { } reservation)
        {
            http.Features.Set(new BrowserLogoutRequest(state!, reservation));
        }
        return choice == BrowserLogoutRequest.Issuer ? BrowserLogoutPage.Incomplete(http) : BrowserLogoutPage.Committed(http);
    }

    /// <summary>Admits current positive generations normally, or the original proof of exactly one verified logout through a distinct CSRF purpose.</summary>
    private async Task<(BrowserGenerationProof Proof, bool Reconciliation)?> ReadAsync(HttpContext http)
    {
        if (await login.ReadAsync(http, create: false) is { Generation: > 0 } current)
        {
            return (current, false);
        }
        if (!BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Generation, out var raw) ||
            proofs.Generation(raw, clock.GetUtcNow()) is not { } proof ||
            await new BrowserLogoutReconciliation(database, clock).ReadAsync(proof.Digest, proof.Generation, http.RequestAborted) is not { } result ||
            BrowserRequestBinding.FromLogoutReconciliation(proof, result, clock.GetUtcNow()) is not { } binding)
        {
            return null;
        }
        http.Features.Set(binding);
        http.Features.Set(new BrowserLoginPublication(result.ObservedAt, result.ExpiresAt, new BrowserLoginPublication(proof.Created, proof.Expires)));
        return (proof, true);
    }

    /// <summary>Stages cookie clearing and the immutable committed generation; outer disposal or expiry still discards every staged cookie.</summary>
    private void Publish(HttpContext http, BrowserGenerationProof previous, BrowserLogoutResult result)
    {
        http.Features.Set(new BrowserLoginPublication(result.ObservedAt, result.ExpiresAt, http.Features.Get<BrowserLoginPublication>()));
        var proof = new BrowserGenerationProof(previous.Browser, result.Generation, result.ObservedAt, result.ExpiresAt);
        var options = BrowserSessionCookies.Bounded(clock.GetUtcNow(), new DateTimeOffset(result.ExpiresAt, TimeSpan.Zero))
            ?? throw new InvalidOperationException(BrowserLoginBoundary.Unavailable);
        var encoded = proofs.Protect(proof);
        http.Features.Set(new BrowserLogoutCookie(encoded, result.ExpiresAt));
        http.Response.Cookies.Append(BrowserSessionCookies.Generation, encoded, options);
        foreach (var name in new[] { BrowserSessionCookies.Session, BrowserSessionCookies.Prelogin, BrowserSessionCookies.Antiforgery, BrowserSessionCookies.Logout })
        {
            http.Response.Cookies.Delete(name, BrowserSessionCookies.SessionOptions());
        }
    }
}
