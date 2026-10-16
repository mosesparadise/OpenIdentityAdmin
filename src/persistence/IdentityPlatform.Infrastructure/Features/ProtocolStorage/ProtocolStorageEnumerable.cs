namespace IdentityPlatform.Infrastructure.Features.ProtocolStorage;

/// <summary>Separates native reader lifetime from exclusive execution so manager relationship getters remain valid between yields.</summary>
internal sealed class ProtocolStorageEnumerable<T>(ProtocolStorageOwner owner, ProtocolStoragePhase phase,
    Func<CancellationToken, IAsyncEnumerable<T>> source, CancellationToken caller) : IAsyncEnumerable<T>
{
    /// <summary>Enlists only an actual enumerator and preserves source, phase and enumeration cancellation until disposal.</summary>
    public IAsyncEnumerator<T> GetAsyncEnumerator(CancellationToken cancellationToken = default) =>
        new Enumerator(owner, phase, source, caller, cancellationToken);

    private sealed class Enumerator : IAsyncEnumerator<T>
    {
        private readonly ProtocolStorageOwner owner;
        private readonly ProtocolStoragePhase phase;
        private readonly Func<CancellationToken, IAsyncEnumerable<T>> source;
        private readonly CancellationTokenSource cancellation;
        private readonly object sync = new();
        private IAsyncEnumerator<T>? native;
        private Task<bool>? move;
        private Task? disposal;
        public T Current
        {
            /// <summary>Checks epoch and cancellation before exposing a native row.</summary>
            get
            {
                owner.Check(phase, cancellation.Token);
                return native!.Current;
            }
        }

        /// <summary>Binds the captured epoch before registering this reader without invoking any deferred native source yet.</summary>
        internal Enumerator(ProtocolStorageOwner owner, ProtocolStoragePhase phase,
            Func<CancellationToken, IAsyncEnumerable<T>> source, CancellationToken caller, CancellationToken enumerator)
        {
            this.owner = owner;
            this.phase = phase;
            this.source = source;
            CancellationTokenSource? created = null;
            try
            {
                owner.Check(phase, caller);
                cancellation = created = CancellationTokenSource.CreateLinkedTokenSource(phase.Lifetime.Token, caller, enumerator);
                owner.Check(phase, cancellation.Token);
                owner.AddReader(phase, this);
            }
            catch { created?.Dispose(); owner.Fail(); throw; }
        }

        /// <summary>Rejects overlapping reader execution while permitting an unrelated sequential getter after a yield.</summary>
        public ValueTask<bool> MoveNextAsync()
        {
            lock (sync)
            {
                if (disposal is not null || (move is not null && !move.IsCompleted))
                {
                    owner.Fail();
                    return ValueTask.FromException<bool>(ProtocolStorageOwner.Unavailable());
                }
                move = MoveCoreAsync();
                return new(move);
            }
        }

        /// <summary>Gates native source creation and each actual MoveNext, checking time and ownership before exposing a row.</summary>
        private async Task<bool> MoveCoreAsync() => await owner.RunAsync(phase, async token =>
        {
            native ??= source(cancellation.Token).GetAsyncEnumerator(cancellation.Token);
            var next = await native.MoveNextAsync();
            token.ThrowIfCancellationRequested();
            return next;
        }, cancellation.Token);

        /// <summary>Creates exactly one disposal task so scope cleanup can join an already requested reader disposal safely.</summary>
        public ValueTask DisposeAsync()
        {
            lock (sync)
            {
                disposal ??= DisposeCoreAsync(move);
                return new(disposal);
            }
        }

        /// <summary>Cancels and awaits actual reader execution before native disposal; cleanup owns no concurrent EF work.</summary>
        private async Task DisposeCoreAsync(Task<bool>? pending)
        {
            await Task.Yield();
            List<Exception> failures = [];
            try
            {
                try
                {
                    await cancellation.CancelAsync();
                }
                catch (Exception error) { owner.Fail(); failures.Add(error); }
                if (pending is not null)
                {
                    try
                    {
                        await pending;
                    }
                    catch { owner.Fail(); }
                }
                using var execution = await owner.EnterCleanupAsync(phase);
                if (native is not null)
                {
                    await native.DisposeAsync();
                }
            }
            catch (Exception error) { owner.Fail(); failures.Add(error); }
            finally { owner.RemoveReader(this); cancellation.Dispose(); }
            if (failures.Count != 0)
            {
                throw new AggregateException(failures);
            }
        }
    }
}
