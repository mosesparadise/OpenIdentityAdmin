using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.AuthServer.Features.Accounts.Login;

public sealed class LoginInteractions(PlatformDbContext database, UserManager<PlatformUser> users,
    SignInManager<PlatformUser> signIn, PilotEligibility eligibility, DummyPassword dummy, TimeProvider time)
{
    public async Task<LoginInteraction?> BoundAsync(HttpContext context, string? id)
    {
        if (!InteractionSecrets.IsIdentifier(id))
        {
            return null;
        }
        var interaction = await database.Set<LoginInteraction>().AsNoTracking().SingleOrDefaultAsync(value => value.Id == id, context.RequestAborted);
        return interaction is not null && !interaction.Consumed && interaction.ExpiresAt > Now &&
            InteractionSecrets.Matches(context.Request.Cookies[PilotProfile.CorrelationPrefix + id], interaction.BrowserDigest)
                ? interaction : null;
    }

    public async Task<LoginInteraction?> FindAsync(string requestTokenId, CancellationToken cancellationToken) =>
        await database.Set<LoginInteraction>().AsNoTracking().SingleOrDefaultAsync(value => value.RequestTokenId == requestTokenId, cancellationToken);

    public async Task<LoginInteraction?> CreateAsync(HttpContext context, string tokenId, string requestUri, long nativeExpiry)
    {
        var candidates = context.Request.Cookies.Keys.Where(name => name.StartsWith(PilotProfile.CorrelationPrefix, StringComparison.Ordinal)).Take(9).ToArray();
        if (candidates.Length > 8)
        {
            return null;
        }
        var ids = candidates.Select(name => name[PilotProfile.CorrelationPrefix.Length..]).Where(InteractionSecrets.IsIdentifier).ToArray();
        var active = await database.Set<LoginInteraction>().AsNoTracking()
            .Where(value => ids.Contains(value.Id) && !value.Consumed && value.ExpiresAt > Now)
            .ToDictionaryAsync(value => value.Id, context.RequestAborted);
        var count = 0;
        foreach (var cookie in candidates)
        {
            var id = cookie[PilotProfile.CorrelationPrefix.Length..];
            if (active.TryGetValue(id, out var current) && InteractionSecrets.Matches(context.Request.Cookies[cookie], current.BrowserDigest))
            {
                count++;
            }
            else
            {
                context.Response.Cookies.Delete(cookie, CookieOptions());
            }
        }
        if (count >= 8 || tokenId.Length > 100 || requestUri.Length > 512 || nativeExpiry <= Now)
        {
            return null;
        }
        var secret = InteractionSecrets.Create();
        var interaction = new LoginInteraction
        {
            Id = InteractionSecrets.Create(),
            RequestTokenId = tokenId,
            RequestUri = requestUri,
            ClientId = PilotProfile.ClientId,
            BrowserDigest = InteractionSecrets.Digest(secret),
            CreatedAt = Now,
            ExpiresAt = Math.Min(Now + PilotProfile.InteractionSeconds, nativeExpiry)
        };
        database.Add(interaction);
        await database.SaveChangesAsync(context.RequestAborted);
        context.Response.Cookies.Append(PilotProfile.CorrelationPrefix + interaction.Id, secret, CookieOptions());
        return interaction;
    }

    public async Task<LocalIdentitySession?> CompleteAsync(LoginInteraction interaction, string username, string password, string? previousSessionId, CancellationToken cancellationToken)
    {
        if (interaction.CompletedSessionId is not null || username.Length is < 1 or > 128 || password.Length is < 1 or > 256)
        {
            return null;
        }
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var user = await users.FindByNameAsync(username);
        var eligible = user is not null && await eligibility.UserAsync(user, cancellationToken);
        var verified = false;
        if (eligible)
        {
            verified = (await signIn.CheckPasswordSignInAsync(user!, password, lockoutOnFailure: true)).Succeeded;
        }
        else
        {
            dummy.Verify(password);
        }
        cancellationToken.ThrowIfCancellationRequested();
        if (!verified)
        {
            LoginAudit.Add(database, "password-verification", "denied", Now, user?.Id, "anonymous");
            await database.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        var session = new LocalIdentitySession
        {
            Id = InteractionSecrets.Create(),
            UserId = user!.Id,
            SecurityStamp = user.SecurityStamp!,
            AuthenticatedAt = Now,
            ExpiresAt = Now + PilotProfile.SessionSeconds
        };
        var completed = await database.Set<LoginInteraction>()
            .Where(value => value.Id == interaction.Id && !value.Consumed && value.CompletedSessionId == null && value.ExpiresAt > Now)
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.CompletedSessionId, session.Id)
                .SetProperty(value => value.CompletedUserId, session.UserId), cancellationToken);
        if (completed != 1)
        {
            return null;
        }
        if (previousSessionId is not null)
        {
            await database.Set<LocalIdentitySession>().Where(value => value.Id == previousSessionId)
                .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Revoked, true), cancellationToken);
        }
        database.Add(session);
        LoginAudit.Add(database, "password-verification", "session-created", Now, user.Id);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return session;
    }

    public async Task<bool> ConsumeAsync(LoginInteraction interaction, LocalIdentitySession session, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken);
        var count = await database.Set<LoginInteraction>().Where(value => value.Id == interaction.Id && !value.Consumed &&
            value.ExpiresAt > Now && (value.CompletedSessionId == null ||
                value.CompletedSessionId == session.Id && value.CompletedUserId == session.UserId))
            .ExecuteUpdateAsync(setters => setters.SetProperty(value => value.Consumed, true), cancellationToken);
        if (count != 1)
        {
            return false;
        }
        LoginAudit.Add(database, "authorization-decision", "approved-for-native-processing", Now, session.UserId);
        await database.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    public static string Resume(LoginInteraction interaction) => "/connect/authorize" + QueryString.Create(
        new Dictionary<string, string?> { ["client_id"] = interaction.ClientId, ["request_uri"] = interaction.RequestUri });

    public static CookieOptions CookieOptions() => new()
    {
        Secure = true,
        HttpOnly = true,
        Path = "/",
        SameSite = SameSiteMode.Lax,
        MaxAge = TimeSpan.FromSeconds(PilotProfile.InteractionSeconds)
    };

    private long Now => time.GetUtcNow().ToUnixTimeSeconds();
}
