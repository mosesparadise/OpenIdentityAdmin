namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Owns one local initialization attempt and fresh dashboard interaction for one disposable run.</summary>
internal sealed class DemoCommand(IDemoProcess process, string run)
{
    internal const string Name = "initialize-administrator";
    private const int Ready = 0;
    private const int Busy = 1;
    private const int Terminal = 2;
    private const string Title = "Initialize administrator";
    private const string Prompt = "Create a synthetic administrator for this disposable run. Password/MFA enrollment is still required.";
    private const string NotReady = "Initialization is unavailable. Wait for the initializer to run, or start a new demo if initialization was already attempted.";
    private const string Rejected = "Initialization was not submitted. Check the inputs and resource readiness.";
    private const string Failed = "Initialization did not complete. Stop the AppHost and start a fresh disposable run; no operation was retried.";
    private const string CleanupFailed = "Initialization did not complete and initializer cleanup could not be confirmed. Stop the AppHost and inspect its owned resources before starting a fresh run.";
    private const string Completed = "Administrator initialized. Open https://issuer.dev.localhost:17443/account/administrator/enrollment to enroll with password and MFA, then https://bff.dev.localhost:17444/bff/login.";
    private static readonly TimeSpan InteractionTimeout = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(20);
    private int state;
    internal bool Available => Volatile.Read(ref state) == Ready;

    /// <summary>Admits a single callback, denies unsafe context and clears all prompt values after use.</summary>
    internal async Task<ExecuteCommandResult> ExecuteAsync(IInteractionService interaction, Func<bool> isRunning, CancellationToken token, Func<Task>? publishState = null)
    {
        if (Interlocked.CompareExchange(ref state, Busy, Ready) != Ready)
        {
            return Failure(NotReady);
        }
        var initializer = new DemoInitializer(process, run);
        using var inputs = new DemoInputs();
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(InteractionTimeout);
        try
        {
            if (publishState is not null)
            {
                await publishState();
            }
            if (!interaction.IsAvailable || !isRunning())
            {
                return Failure(NotReady);
            }
            await initializer.BindAsync(deadline.Token);
            var response = await interaction.PromptInputsAsync(Title, Prompt, inputs.Fields, cancellationToken: deadline.Token);
            if (response.Canceled)
            {
                return new ExecuteCommandResult { Success = false, Canceled = true };
            }
            var bootstrap = inputs.Encode(response.Data);
            if (!isRunning())
            {
                return Failure(Rejected);
            }
            await initializer.InitializeAsync(bootstrap, deadline.Token);
            return new ExecuteCommandResult { Success = true, Message = Completed };
        }
        catch (Exception)
        {
            return await HandleFailureAsync(initializer);
        }
        finally
        {
            Volatile.Write(ref state, initializer.Attempted ? Terminal : Ready);
            if (publishState is not null)
            {
                await publishState();
            }
        }
    }

    /// <summary>Uses a fresh cleanup budget because command cancellation must not leave remote exec running.</summary>
    private static async Task<ExecuteCommandResult> HandleFailureAsync(DemoInitializer initializer)
    {
        if (!initializer.Attempted)
        {
            return Failure(Rejected);
        }
        using var cleanup = new CancellationTokenSource(CleanupTimeout);
        try
        {
            await initializer.StopAsync(cleanup.Token);
        }
        catch (Exception) { return Failure(CleanupFailed); }
        return Failure(Failed);
    }

    /// <summary>Returns only feature-owned finite messages, never command output or exception text.</summary>
    private static ExecuteCommandResult Failure(string message) => new() { Success = false, Message = message };
}
