using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Separately witnesses one real BFF pipeline return and its server response completion without retaining request content.</summary>
internal sealed class ApplicationRequestCompletion(string path, Action detach) : IDisposable
{
    private const int DuplicateCount = 2;
    private readonly object gate = new();
    private readonly TaskCompletionSource pipeline = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly TaskCompletionSource response = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int requests;
    private bool invalid;
    private bool sealedObservation;
    private Attempt? original;
    internal Task Completion => Task.WhenAll(pipeline.Task, response.Task);
    internal bool PipelineCompleted => pipeline.Task.IsCompletedSuccessfully;
    internal bool ResponseCompleted => response.Task.IsCompletedSuccessfully;
    internal int Requests => Volatile.Read(ref requests);
    internal int Status
    {
        get; private set;
    }
    internal bool ResponseStarted
    {
        get; private set;
    }
    internal bool RequestAborted
    {
        get; private set;
    }

    /// <summary>Arms only the exact query-free POST, with a private attempt identity shared by the two independent completion signals.</summary>
    internal Attempt? Enter(HttpContext http)
    {
        if (http.Request.Path != path || !HttpMethods.IsPost(http.Request.Method))
        {
            return null;
        }
        lock (gate)
        {
            requests = Math.Min(DuplicateCount, requests + 1);
            invalid |= sealedObservation || requests != 1 || http.Request.QueryString.HasValue;
            var attempt = new Attempt(this);
            original ??= attempt;
            http.Response.OnCompleted(static state =>
            {
                ((Attempt)state).ResponseEnded();
                return Task.CompletedTask;
            }, attempt);
            return attempt;
        }
    }

    /// <summary>Accepts each completion exactly once and only for the single original attempt.</summary>
    private void End(Attempt attempt, bool responseEnded, int status = 0, bool started = false, bool aborted = false)
    {
        lock (gate)
        {
            var target = responseEnded ? response : pipeline;
            invalid |= sealedObservation || !ReferenceEquals(original, attempt) || target.Task.IsCompleted;
            if (!responseEnded)
            {
                Status = status;
                ResponseStarted = started;
                RequestAborted = aborted;
            }
            target.TrySetResult();
        }
    }

    /// <summary>Detaches admission before final cardinality and completion validation; late callbacks invalidate the same ledger.</summary>
    internal bool Seal()
    {
        detach();
        lock (gate)
        {
            invalid |= sealedObservation;
            sealedObservation = true;
            return !invalid && requests == 1 && original is not null && PipelineCompleted && ResponseCompleted;
        }
    }

    /// <summary>Detaches only this observation; it cannot finish either server operation.</summary>
    public void Dispose() => detach();

    /// <summary>Contains no HTTP context or payload and cannot transfer completion between attempts.</summary>
    internal sealed class Attempt(ApplicationRequestCompletion owner)
    {
        /// <summary>Called only by the actual outer pipeline finally.</summary>
        internal void PipelineEnded(int status, bool started, bool aborted) => owner.End(this, responseEnded: false, status, started, aborted);
        /// <summary>Called only by the server's registered response-completion callback.</summary>
        internal void ResponseEnded() => owner.End(this, responseEnded: true);
    }
}
