using System.Runtime.ExceptionServices;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Restores test-owned runtime read rights even when revocation completion is unknown or the expected failure control fails.</summary>
internal static class NativeClientStateFailure
{
    private const int CleanupSeconds = 5;

    /// <summary>Arms recovery before attempting revocation and preserves both primary and restoration failures without retry.</summary>
    internal static async Task DuringDeniedReadAsync(BootstrapSessionAccess access, Func<Task> operation)
    {
        Exception? failure = null;
        try
        {
            await access.SetReadAsync(false, TestContext.Current.CancellationToken);
            var denied = await Record.ExceptionAsync(() => access.ReadAsync(TestContext.Current.CancellationToken));
            Assert.True(access.IsDenied(denied), denied?.GetType().FullName);
            await operation();
        }
        catch (Exception error) { failure = error; }
        finally
        {
            using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
            try
            {
                await access.SetReadAsync(true, cleanup.Token);
            }
            catch (Exception error) { failure = failure is null ? error : new AggregateException(failure, error); }
        }
        if (failure is not null)
        {
            ExceptionDispatchInfo.Capture(failure).Throw();
        }
        await access.ReadAsync(TestContext.Current.CancellationToken);
    }
}
