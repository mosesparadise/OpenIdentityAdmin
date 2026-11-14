namespace IdentityPlatform.Sample.Client.Features.Sessions;

/// <summary>Owns one original request deadline, shortened by browser/session expiry and retained through response publication.</summary>
internal sealed class RequestBudget : IDisposable
{
    private readonly HttpContext context;
    private readonly TimeProvider clock;
    private readonly CancellationTokenSource deadline;
    private readonly CancellationTokenRegistration abort;
    private DateTimeOffset expires;

    /// <summary>Links caller cancellation and a finite operation lifetime before native middleware or any remote work begins.</summary>
    internal RequestBudget(HttpContext context)
    {
        this.context = context;
        clock = context.RequestServices.GetRequiredService<TimeProvider>();
        expires = clock.GetUtcNow().Add(SampleProfile.OperationLifetime);
        deadline = CancellationTokenSource.CreateLinkedTokenSource(context.RequestAborted);
        deadline.CancelAfter(SampleProfile.OperationLifetime);
        context.RequestAborted = deadline.Token;
        context.Features.Set(this);
        abort = deadline.Token.Register(context.Abort);
        context.Response.OnStarting(() =>
        {
            Check();
            return Task.CompletedTask;
        });
    }

    /// <summary>Only shortens the original deadline; a completed remote call or gate acquisition never resets the operation budget.</summary>
    internal void RestrictTo(DateTimeOffset bound)
    {
        if (bound < expires)
        {
            expires = bound;
            var remaining = bound - clock.GetUtcNow();
            if (remaining <= TimeSpan.Zero)
            {
                deadline.Cancel();
            }
            else
            {
                deadline.CancelAfter(remaining);
            }
        }
        Check();
    }

    /// <summary>Checks both cancellation and absolute clock at publication, including deterministic fake-clock regression tests.</summary>
    private void Check()
    {
        if (clock.GetUtcNow() >= expires)
        {
            deadline.Cancel();
        }
        context.RequestAborted.ThrowIfCancellationRequested();
    }

    /// <summary>Bounds caller waiting while retaining an observer for native operations that finish after cancellation.</summary>
    internal static async Task AwaitAsync(Task operation, CancellationToken cancellationToken)
    {
        _ = operation.ContinueWith(static completed => _ = completed.Exception, CancellationToken.None,
            TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
        await operation.WaitAsync(cancellationToken);
    }

    /// <summary>Releases timer/abort registrations only after the request and its guarded publication have finished.</summary>
    public void Dispose()
    {
        abort.Dispose();
        deadline.Dispose();
    }
}
