using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using IdentityPlatform.Administrator.Tests.Fixtures.Governance;
using IdentityPlatform.Administrator.Tests.Fixtures.Processes;

namespace IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;

/// <summary>Owns one separate native client process and credits its private response only after exact process-group and stream termination.</summary>
internal static class NativeClientProcess
{
    private const int FrameLimit = 65_536;
    private const int JsonDepth = 8;
    private const int FailureExit = 2;
    private const string StageProperty = "stage";
    private const string FailureMessage = "Native client child protocol failed; private response and diagnostics are withheld.";
    private static readonly byte[] Newline = "\n"u8.ToArray();
    private static readonly JsonSerializerOptions Options = new()
    {
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        MaxDepth = JsonDepth
    };

    /// <summary>Sends one bounded synthetic request, requires real readiness/result/exit, and returns only in-memory protocol material.</summary>
    internal static async Task<NativeClientProcessOutcome> RunAsync(NativeClientProcessInput input)
    {
        await using var process = new DriverProcess(GovernanceDriverControl.None, DriverExecutable.NativeClient);
        var bytes = JsonSerializer.SerializeToUtf8Bytes(input);
        try
        {
            Assert.True(bytes.Length < FrameLimit, FailureMessage);
            await WriteAsync(process, bytes);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
        using var ready = await process.ReadFrameAsync(DriverProcess.Ready);
        using var result = await process.ReadFrameAsync(DriverProcess.Result);
        NativeClientProcessReply reply;
        try
        {
            reply = result.RootElement.Deserialize<NativeClientProcessReply>(Options) ?? throw new InvalidOperationException();
        }
        catch (Exception) { throw new InvalidOperationException(FailureMessage); }
        await process.RequireExitAsync(0);
        process.RequireAllTerminated();
        return new(reply, process.Generation);
    }

    /// <summary>Proves input rejection or the missing-key composition control stops at its exact stage, without readiness or raw diagnostics.</summary>
    internal static async Task RequireFailureAsync(byte[] frame, NativeClientProcessFailureStage expectedStage)
    {
        await using var process = new DriverProcess(GovernanceDriverControl.None, DriverExecutable.NativeClient);
        await WriteAsync(process, frame);
        using var failure = await process.ReadFrameAsync(DriverProcess.Unavailable);
        Assert.True(failure.RootElement.TryGetProperty(StageProperty, out var stage) &&
            stage.ValueKind == JsonValueKind.Number && stage.GetInt32() == (int)expectedStage);
        await process.RequireExitAsync(FailureExit);
        process.RequireAllTerminated();
    }

    /// <summary>Closes input after exactly one caller-supplied frame so the child can independently reject trailing data before execution.</summary>
    private static async Task WriteAsync(DriverProcess process, byte[] frame)
    {
        await process.Input.WriteAsync(frame, process.Token);
        await process.Input.WriteAsync(Newline, process.Token);
        await process.Input.FlushAsync(process.Token);
        process.CloseInput();
    }
}

/// <summary>Independently names the expected private child failure stages for framing and missing-credential controls.</summary>
internal enum NativeClientProcessFailureStage
{
    Input,
    Request
}

/// <summary>Keeps private response values out of generated record formatting while retaining independently observed OS process identity.</summary>
internal sealed class NativeClientProcessOutcome(NativeClientProcessReply reply, ProcessGeneration generation)
{
    internal NativeClientProcessReply Reply => reply;
    internal ProcessGeneration Generation => generation;
}
