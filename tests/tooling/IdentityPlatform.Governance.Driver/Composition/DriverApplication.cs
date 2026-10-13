using System.Text.Json;
using IdentityPlatform.Governance.Driver.Features.Operations;
using IdentityPlatform.Governance.Driver.Features.Protocol;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Governance.Driver.Composition;

/// <summary>Runs one bounded synthetic request with no secret arguments, environment configuration, HTTP listener or logging sink.</summary>
internal static class DriverApplication
{
    private const int FailureOutputSeconds = 1;

    /// <summary>Contains every ordinary failure behind a fixed frame and nonzero exit code; completion may remain indeterminate.</summary>
    internal static async Task<int> RunAsync(string[] args)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(DriverLimits.ProcessSeconds));
        var stage = DriverStage.Input;
        try
        {
            if (args.Length != 0 || !Console.IsInputRedirected || !Console.IsOutputRedirected)
            {
                throw new InvalidOperationException();
            }
            using var input = Console.OpenStandardInput();
            var request = await DriverProtocol.ReadAsync(input, deadline.Token);
            stage = DriverStage.Composition;
            var result = await ExecuteAsync(request, value => stage = value, deadline.Token);
            stage = DriverStage.Delivery;
            if (result.AcceptedCommit)
            {
                await DriverProtocol.CommittedAsync(deadline.Token);
                if (request.AwaitAcknowledgement)
                {
                    await DriverProtocol.AcknowledgeAsync(input, deadline.Token);
                }
            }
            await DriverProtocol.WriteAsync(result.Frame, deadline.Token);
            return DriverLimits.Success;
        }
        catch (Exception exception)
        {
            await TryReportFailureAsync(stage, Classify(exception));
            return DriverLimits.Failure;
        }
    }

    /// <summary>Classifies startup failures without exposing native messages, file paths or caller values.</summary>
    private static DriverFailure Classify(Exception exception) => exception switch
    {
        AggregateException => DriverFailure.Dependency,
        FileNotFoundException => DriverFailure.MissingAssembly,
        FileLoadException => DriverFailure.AssemblyLoad,
        TypeLoadException => DriverFailure.TypeLoad,
        MissingMethodException => DriverFailure.MissingMethod,
        ArgumentException => DriverFailure.ConfigurationArgument,
        _ => DriverFailure.Runtime
    };

    /// <summary>Emits readiness only after valid issuer protection, and releases scopes before indicating completion.</summary>
    private static async Task<DriverResult> ExecuteAsync(DriverInput request, Action<DriverStage> observe, CancellationToken token)
    {
        await using var runtime = request.ConfigureServices(observe);
        await using var scope = runtime.Provider.CreateAsyncScope();
        observe(DriverStage.Operation);
        await DriverProtocol.ReadyAsync(token);
        return await scope.ServiceProvider.GetRequiredService<DriverOperation>().RunAsync(request, token);
    }

    /// <summary>Attempts bounded fixed output without allowing broken pipes or cleanup errors to print sensitive diagnostics.</summary>
    private static async Task TryReportFailureAsync(DriverStage stage, DriverFailure failure)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(FailureOutputSeconds));
        try
        {
            const string Unavailable = "unavailable";
            await DriverProtocol.WriteAsync(JsonSerializer.Serialize(new
            {
                phase = Unavailable,
                stage,
                failure
            }) + DriverLimits.Newline, deadline.Token);
        }
        catch (Exception) { /* The parent still observes process exit; no fallback diagnostic is safe. */ }
    }
}

/// <summary>Fixed diagnostic stages contain no caller data or native exception text.</summary>
internal enum DriverStage
{
    Input, Composition, Credentials, ServiceValidation, Protection, Operation, Delivery
}

/// <summary>Fixed diagnostic classes distinguish invalid service registration from other unavailable completion.</summary>
internal enum DriverFailure
{
    Runtime, Dependency, MissingAssembly, AssemblyLoad, TypeLoad, MissingMethod, ConfigurationArgument
}
