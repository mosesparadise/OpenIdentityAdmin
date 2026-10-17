namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Guards installed native dispatch while preserving nested transaction cancellation bindings and native outcomes.</summary>
internal static class ProtocolStorageDispatch
{
    /// <summary>Awaits the actual dispatcher, restores its shared transaction token, and leaves whole-flow cleanup to the request owner.</summary>
    internal static async ValueTask RunAsync(ProtocolStorageOwner owner, Func<CancellationToken> readToken,
        Action<CancellationToken> writeToken, Func<ValueTask> dispatch)
    {
        var frame = owner.BeginDispatch();
        var ordered = true;
        try
        {
            var original = readToken();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(original, owner.RequestToken);
            writeToken(linked.Token);
            try
            {
                linked.Token.ThrowIfCancellationRequested();
                await dispatch();
                if (readToken() != linked.Token)
                {
                    throw ProtocolStorageOwner.Unavailable();
                }
                linked.Token.ThrowIfCancellationRequested();
                owner.RequireHealthy();
            }
            finally { writeToken(original); }
        }
        catch { owner.Fail(); throw; }
        finally
        {
            ordered = owner.Dispatches.Exit(frame);
            if (!ordered)
            {
                owner.Fail();
            }
        }
        if (!ordered)
        {
            throw ProtocolStorageOwner.Unavailable();
        }
    }
}
