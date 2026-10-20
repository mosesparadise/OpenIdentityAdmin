using IdentityPlatform.NativeClient.Driver.Features.Protocol;
using IdentityPlatform.NativeClient.Driver.Features.Requests;

namespace IdentityPlatform.NativeClient.Driver.Composition;

/// <summary>Runs one bounded private native-client request without ambient configuration, arbitrary commands or credential-bearing diagnostics.</summary>
internal static class NativeClientDriverApplication
{
    private const int FailureOutputSeconds = 1;
    private const string Unavailable = "unavailable";

    /// <summary>Contains ordinary exceptions behind finite stage/type values; successful output follows complete service/key cleanup.</summary>
    internal static async Task<int> RunAsync(string[] args)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(NativeClientDriverProtocol.ProcessSeconds));
        var stage = NativeClientDriverStage.Input;
        try
        {
            if (args.Length != 0 || !Console.IsInputRedirected || !Console.IsOutputRedirected)
            {
                throw new InvalidOperationException();
            }
            var input = await NativeClientDriverProtocol.ReadAsync(deadline.Token);
            stage = NativeClientDriverStage.Request;
            var result = await NativeClientDriverRequest.RunAsync(input, deadline.Token);
            stage = NativeClientDriverStage.Delivery;
            await NativeClientDriverProtocol.WriteAsync(result, deadline.Token);
            return 0;
        }
        catch (Exception error)
        {
            using var output = new CancellationTokenSource(TimeSpan.FromSeconds(FailureOutputSeconds));
            try
            {
                await NativeClientDriverProtocol.WriteAsync(new
                {
                    phase = Unavailable,
                    stage,
                    failure = error is OperationCanceledException ? NativeClientDriverFailure.Cancelled : NativeClientDriverFailure.Runtime
                }, output.Token);
            }
            catch (Exception) { /* The parent still observes exit; no raw diagnostic fallback is permitted. */ }
            return NativeClientDriverProtocol.FailureExit;
        }
    }
}

/// <summary>Names finite child stages without echoing connection, profile, file or credential values.</summary>
internal enum NativeClientDriverStage
{
    Input, Request, Delivery
}

/// <summary>Distinguishes cancellation from other unavailable completion without treating either as rollback.</summary>
internal enum NativeClientDriverFailure
{
    Runtime, Cancelled
}
