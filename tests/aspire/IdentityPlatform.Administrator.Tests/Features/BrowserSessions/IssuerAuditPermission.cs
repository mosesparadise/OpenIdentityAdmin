using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Owns disposable audit permission denial and bounded independent restoration without suppressing the original failure.</summary>
internal static class IssuerAuditPermission
{
    private const int CleanupSeconds = 10;

    /// <summary>Restores even an ambiguously completed denial; cancellation of the test cannot abandon fixture permission cleanup.</summary>
    internal static async Task RunAsync(BootstrapDatabase database, Func<Task> action)
    {
        ExceptionDispatchInfo? primary = null;
        try
        {
            await database.AuditPermissionAsync(false, TestContext.Current.CancellationToken);
            await action();
        }
        catch (Exception error) { primary = ExceptionDispatchInfo.Capture(error); }
        ExceptionDispatchInfo? cleanup = null;
        using (var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds)))
        {
            try
            {
                await database.AuditPermissionAsync(true, deadline.Token);
            }
            catch (Exception error) { cleanup = ExceptionDispatchInfo.Capture(error); }
        }
        if (primary is not null && cleanup is not null)
        {
            throw new AggregateException(primary.SourceException, cleanup.SourceException);
        }
        primary?.Throw();
        cleanup?.Throw();
    }
}
