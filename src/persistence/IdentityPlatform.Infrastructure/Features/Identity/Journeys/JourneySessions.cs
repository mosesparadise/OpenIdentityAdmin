using System.Data;
using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace IdentityPlatform.Infrastructure.Features.Identity.Journeys;

/// <summary>Revalidates stored account authority on every protected request without refreshing authentication proof.</summary>
internal sealed class JourneySessions(PlatformDbContext database, NativeOperation operation, CeremonyClock clock,
    UserManager<PlatformUser> users)
{
    /// <summary>Serializes against mutations, validates current protected material and commits only monotonic activity metadata.</summary>
    internal async Task<JourneySession?> ValidateAsync(string userId, string publicId, CancellationToken token)
    {
        if (string.IsNullOrEmpty(userId) || userId.Length > FactorSchema.UserLength || !CeremonySecrets.IsIdentifier(publicId))
        {
            return null;
        }
        JourneySession result;
        await using (var transaction = await SessionScope.OpenAsync(database, operation, userId, token))
        {
            ((AdministratorUserManager)users).RequireOperation(transaction.Token);
            var user = await database.Users.SingleOrDefaultAsync(row => row.Id == userId, transaction.Token);
            var enrollment = await database.Set<AdministratorEnrollment>().SingleOrDefaultAsync(row => row.UserId == userId, transaction.Token);
            var authentication = await database.Set<AdministratorAuthentication>().SingleOrDefaultAsync(row => row.UserId == userId, transaction.Token);
            var session = await database.Set<AdministratorSession>().SingleOrDefaultAsync(row => row.PublicId == publicId, transaction.Token);
            if (user is null || enrollment is null || authentication is null || session is null || session.PublicId != publicId ||
                user.Id != userId || enrollment.UserId != userId || authentication.UserId != userId || session.UserId != userId ||
                !clock.Observe(authentication, out var startedAt) || !JourneySessionState.Allows(user, enrollment, authentication, session, startedAt) ||
                string.IsNullOrEmpty(await users.GetAuthenticatorKeyAsync(user)) ||
                !clock.Observe(authentication, out var now) || !JourneySessionState.Allows(user, enrollment, authentication, session, now))
            {
                return null;
            }
            session.LastActivityAt = Math.Max(session.LastActivityAt, now);
            session.Revision = checked(session.Revision + 1);
            result = new JourneySession(session.PublicId, userId, session.PasswordVerifiedAt, session.FactorVerifiedAt, session.ExpiresAt);
            await transaction.CommitAsync(session, authentication, startedAt, now);
        }
        return result;
    }

    /// <summary>Isolates metadata ownership inside the validator; callers cannot obtain or commit this transaction.</summary>
    private sealed class SessionScope(PlatformDbContext database, string userId, IDbContextTransaction transaction,
        IDisposable native, CeremonyConnection connection, CancellationTokenSource deadline) : IAsyncDisposable
    {
        internal CancellationToken Token => deadline.Token;

        /// <summary>Acquires the account lock with native cancellation while exposing no ceremony capability or write callback.</summary>
        internal static async Task<SessionScope> OpenAsync(PlatformDbContext database, NativeOperation operation, string userId, CancellationToken token)
        {
            if (database.Database.CurrentTransaction is not null || database.ChangeTracker.HasChanges())
            {
                throw new FactorUnavailableException();
            }
            database.ChangeTracker.Clear();
            var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromSeconds(AdministratorPolicy.OperationSeconds));
            var connection = new CeremonyConnection(database);
            IDbContextTransaction? transaction = null;
            IDisposable? native = null;
            try
            {
                await connection.OpenAsync(deadline.Token);
                transaction = await database.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, deadline.Token);
                native = operation.Enter(deadline.Token);
                await new CeremonyLocks(database).AcquireAsync(new CeremonyLockPlan(false, false, [], [userId]), deadline.Token);
                return new SessionScope(database, userId, transaction, native, connection, deadline);
            }
            catch
            {
                await CeremonyTransaction.ReleaseAsync(database, native, transaction, connection, deadline);
                throw;
            }
        }

        /// <summary>Checks exact owner, validated row and accepted clock sample before committing only the validator's metadata.</summary>
        internal async Task CommitAsync(AdministratorSession session, AdministratorAuthentication authentication, long startedAt, long now)
        {
            if (session.UserId != userId || authentication.UserId != userId ||
                session.LastActivityAt != Math.Max(database.Entry(session).Property(row => row.LastActivityAt).OriginalValue, now) ||
                authentication.ObservedAt != Math.Max(database.Entry(authentication).Property(row => row.ObservedAt).OriginalValue ?? startedAt, Math.Max(startedAt, now)) ||
                database.ChangeTracker.Entries<AdministratorSession>().Any(entry => !ReferenceEquals(entry.Entity, session)) ||
                database.ChangeTracker.Entries<AdministratorAuthentication>().Any(entry => !ReferenceEquals(entry.Entity, authentication)))
            {
                throw new FactorUnavailableException();
            }
            SessionMetadata.Require(database);
            await database.SaveChangesAsync(Token);
            await transaction.CommitAsync(Token);
        }

        /// <summary>Rolls back denial and releases the native lease before disposing all database lifetime owners.</summary>
        public ValueTask DisposeAsync() => CeremonyTransaction.ReleaseAsync(database, native, transaction, connection, deadline);
    }

}

/// <summary>Exposes verified account status without leaking factor material, security stamps or permission catalogues.</summary>
internal sealed record JourneySession(string SessionId, string UserId, long PasswordVerifiedAt, long FactorVerifiedAt, long ExpiresAt);
