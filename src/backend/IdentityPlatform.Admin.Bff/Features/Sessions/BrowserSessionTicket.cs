using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.Sessions.Browser;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;

namespace IdentityPlatform.Admin.Bff.Features.Sessions;

/// <summary>Prepares a fresh canonical B1 ticket and releases its handle only after atomic browser establishment and local scope disposal.</summary>
internal sealed class BrowserSessionTicket(string handle, long created, long expires, string generation)
{
    internal string Handle { get; } = handle;
    internal string Generation { get; } = generation;
    internal long Expires { get; } = expires;

    /// <summary>Uses the existing T005 transaction instead of opening an independent B1 store commit.</summary>
    internal static async Task<BrowserSessionTicket> EstablishAsync(BrowserCallbackReceipt receipt, IServiceScopeFactory factory, TimeProvider clock, CancellationToken token)
    {
        var (admission, identity) = receipt.Completed();
        await using var scope = factory.CreateAsyncScope();
        var codec = scope.ServiceProvider.GetRequiredService<SessionTicketCodec>();
        var ticket = identity.CreateTicket();
        var reference = SessionHandle.Create();
        var row = SessionTicketPolicy.Create(reference.Digest, ticket.Principal.FindFirst(BrowserSessionClaims.Subject)!.Value,
            ticket.Principal.FindFirst(BrowserSessionClaims.Session)!.Value, clock.GetUtcNow(), identity.SourceExpiresAt,
            SessionTicketLimits.MaximumIdle, SessionTicketLimits.MaximumAbsolute);
        row.ProtectedTicket = codec.Protect(row, ticket);
        var proof = new BrowserGenerationProof(admission.Browser.Browser, admission.Browser.Generation, row.CreatedAt, row.AbsoluteExpiresAt);
        var protectedGeneration = scope.ServiceProvider.GetRequiredService<BrowserLoginProof>().Protect(proof);
        receipt.Require();
        var database = scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>();
        if (!await new BrowserLoginCompletion(database, clock, codec).EstablishAsync(admission.Browser.Digest, admission.Pending.Digest,
            admission.Pending.Generation, admission.Reservation, row, token))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        return new(reference.Handle, row.CreatedAt, Math.Min(row.IdleExpiresAt, row.AbsoluteExpiresAt), protectedGeneration);
    }

    /// <summary>Denies original effective ticket expiry and any rollback after local commit without creating a fresh lifetime.</summary>
    internal void Require(TimeProvider clock)
    {
        var now = clock.GetUtcNow().UtcTicks;
        if (now < created || now >= Expires)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
