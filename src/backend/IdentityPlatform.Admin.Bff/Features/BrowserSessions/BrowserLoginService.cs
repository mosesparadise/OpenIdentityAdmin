using IdentityPlatform.Admin.Bff.Features.Sessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Coordinates protected browser admission with existing Sessions transactions; native HTTP starts only after this service's scope is disposed.</summary>
internal sealed class BrowserLoginService(AdminSessionDbContext database, TimeProvider clock, BrowserLoginProof proofs,
    SessionTicketCodec codec, BrowserSessionDestinations destinations, IAntiforgery antiforgery)
{
    /// <summary>Preloads current durable browser state before synchronous native antiforgery without admitting an intent on GET.</summary>
    internal async Task<BrowserGenerationProof?> ReadAsync(HttpContext http, bool create)
    {
        if (!BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Generation, out var raw))
        {
            return null;
        }
        var now = clock.GetUtcNow();
        var proof = raw is null && create ? new BrowserGenerationProof(BrowserLoginSelector.CreateSelector(), 0,
            now.UtcTicks, checked(now.UtcTicks + BrowserSessionPolicy.Lifetime)) : proofs.Generation(raw, now);
        if (proof is null)
        {
            return null;
        }
        var row = await database.Set<BrowserContext>().AsNoTracking().SingleOrDefaultAsync(value => value.Digest == proof.Digest, http.RequestAborted);
        var binding = BrowserRequestBinding.FromRead(proof.Digest, proof.Generation, row, clock.GetUtcNow());
        if (binding is null)
        {
            return null;
        }
        http.Features.Set(binding);
        http.Features.Set(new BrowserLoginPublication(proof.Created, proof.Expires));
        if (raw is null)
        {
            Write(http, BrowserSessionCookies.Generation, proofs.Protect(proof), proof.Expires);
        }
        return proof;
    }

    /// <summary>Requires actual bounded form parsing and native antiforgery before generating or committing a new intent.</summary>
    internal async Task<IResult> StartAsync(HttpContext http)
    {
        if (!await BrowserLoginRequest.FormAsync(http, destinations) || await ReadAsync(http, create: false) is not { } browser)
        {
            return Results.BadRequest();
        }
        await antiforgery.ValidateRequestAsync(http);
        if (!BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Session, out var handle))
        {
            return Results.BadRequest();
        }
        var actor = await ActorAsync(browser, handle, http.RequestAborted);
        if (!actor.Allowed)
        {
            return Results.BadRequest();
        }
        var intent = BrowserLoginSelector.CreateSelector();
        var digest = BrowserLoginSelector.Digest(intent)!;
        var generation = await new BrowserLoginStart(database, clock, codec).StartAsync(browser.Digest, digest,
            browser.Generation, actor.Digest, http.RequestAborted);
        if (generation is null)
        {
            return Results.BadRequest();
        }
        var row = await database.Set<BrowserLoginIntent>().AsNoTracking().SingleAsync(value => value.Digest == digest, http.RequestAborted);
        if (row.Generation != generation || row.State != BrowserIntentState.Pending || row.BrowserDigest != browser.Digest ||
            !BrowserSessionPolicy.Current(row.CreatedAt, row.ObservedAt, row.ExpiresAt, clock.GetUtcNow().UtcTicks))
        {
            return Results.BadRequest();
        }
        var pending = new BrowserPendingProof(intent, browser.Digest, generation.Value, row.CreatedAt, row.ExpiresAt);
        http.Features.Set(new BrowserLoginPublication(row.CreatedAt, row.ExpiresAt, http.Features.Get<BrowserLoginPublication>()));
        Write(http, BrowserSessionCookies.Generation, proofs.Protect(new BrowserGenerationProof(browser.Browser, generation.Value, row.CreatedAt, row.ExpiresAt)), row.ExpiresAt);
        Write(http, BrowserSessionCookies.Prelogin, proofs.Protect(pending), row.ExpiresAt);
        if (handle is not null)
        {
            http.Response.Cookies.Delete(BrowserSessionCookies.Session, BrowserSessionCookies.SessionOptions());
        }
        http.Response.Headers.Location = QueryHelpers.AddQueryString(destinations.Entry.AbsoluteUri, BrowserLoginRequest.Intent, intent);
        return Results.StatusCode(StatusCodes.Status303SeeOther);
    }

    /// <summary>Requires exact protected prelogin and current durable pending state before the single committed native reservation.</summary>
    internal async Task<BrowserLoginReservation?> ResumeAsync(HttpContext http)
    {
        if (BrowserLoginRequest.Resume(http.Request) is not { } completion || !await BrowserLoginRequest.EmptyAsync(http) ||
            await ReadAsync(http, create: false) is not { } browser ||
            !BrowserLoginRequest.Cookie(http.Request, BrowserSessionCookies.Prelogin, out var raw) ||
            proofs.Pending(raw, clock.GetUtcNow()) is not { } pending || pending.BrowserDigest != browser.Digest || pending.Generation != browser.Generation)
        {
            return null;
        }
        var row = await database.Set<BrowserLoginIntent>().AsNoTracking().SingleOrDefaultAsync(value => value.Digest == pending.Digest, http.RequestAborted);
        if (row is null || row.BrowserDigest != pending.BrowserDigest || row.Generation != pending.Generation || row.State != BrowserIntentState.Pending ||
            row.CreatedAt != pending.Created || row.ExpiresAt != pending.Expires ||
            !BrowserSessionPolicy.Current(row.CreatedAt, row.ObservedAt, row.ExpiresAt, clock.GetUtcNow().UtcTicks))
        {
            return null;
        }
        http.Features.Set(new BrowserLoginPublication(pending.Created, pending.Expires, http.Features.Get<BrowserLoginPublication>()));
        return await BrowserLoginReservation.ReserveAsync(http, pending, completion, new BrowserLoginCompletion(database, clock, codec));
    }

    /// <summary>Accepts an audit actor only from a live authentic B1 envelope and the exact current browser/ticket association.</summary>
    private async Task<(bool Allowed, string? Digest)> ActorAsync(BrowserGenerationProof browser, string? handle, CancellationToken token)
    {
        if (handle is null)
        {
            return (true, null);
        }
        if (!SessionHandle.TryDigest(handle, out var digest))
        {
            return (false, null);
        }
        var ticket = await database.Set<AdminSessionTicket>().AsNoTracking().SingleOrDefaultAsync(row => row.Digest == digest, token);
        if (ticket is null || !codec.IsAuthentic(ticket, clock.GetUtcNow()))
        {
            return (true, null);
        }
        var context = await database.Set<BrowserContext>().AsNoTracking().SingleOrDefaultAsync(row => row.Digest == browser.Digest, token);
        return BrowserTicketAcceptance.Accept(handle, browser.Digest, browser.Generation, context, ticket, codec, clock.GetUtcNow())
            ? (true, digest) : (false, null);
    }

    /// <summary>Stages a cookie only after successful local state work; the outer boundary still owns final deadline/disposal admission.</summary>
    private void Write(HttpContext http, string name, string value, long expires) => http.Response.Cookies.Append(name, value,
        BrowserSessionCookies.Bounded(clock.GetUtcNow(), new DateTimeOffset(expires, TimeSpan.Zero)) ?? throw new InvalidOperationException(BrowserLoginBoundary.Unavailable));
}
