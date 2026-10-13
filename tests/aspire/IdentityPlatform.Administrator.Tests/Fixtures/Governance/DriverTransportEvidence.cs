using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Requires exact fixed driver failures, full output exhaustion and genuine exit without exposing child-controlled payloads.</summary>
internal static class DriverTransportEvidence
{
    private const int InputStage = 0;
    private const int CompositionStage = 1;
    private const int RuntimeFailure = 0;
    private const int ConfigurationFailure = 6;
    private const int FailureExit = 2;
    private const string Phase = "phase";
    private const string Stage = "stage";
    private const string Failure = "failure";
    private const string InvalidFrame = "Driver rejection did not contain exactly the fixed safe failure frame.";
    private static readonly string[] Fields = [Failure, Phase, Stage];

    /// <summary>Writes finite synthetic input and closes stdin; only an over-limit frame may race the child's already completed rejection.</summary>
    internal static async Task SubmitAsync(GovernanceDriver child, string payload, bool oversized)
    {
        var bytes = Encoding.UTF8.GetBytes(payload);
        try
        {
            try
            {
                await child.Input.WriteAsync(bytes, child.Token);
                await child.Input.FlushAsync(child.Token);
            }
            catch (IOException) when (oversized)
            {
                // The child can close after consuming the maximum frame before the final newline is written.
                // Exact input-stage failure, stderr silence, EOF and exit below remain mandatory.
            }
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            child.CloseInput();
        }
    }

    /// <summary>Rejects added fields, string diagnostics, unexpected phases and every byte after the one allowed fixed frame.</summary>
    internal static async Task RequireAsync(GovernanceDriver child, bool boundary)
    {
        using var frame = await child.ReadFrameAsync(GovernanceDriver.Unavailable);
        var root = frame.RootElement;
        Assert.True(root.ValueKind == JsonValueKind.Object &&
            root.EnumerateObject().Select(property => property.Name).Order(StringComparer.Ordinal).SequenceEqual(Fields, StringComparer.Ordinal), InvalidFrame);
        Assert.True(root.GetProperty(Stage).ValueKind == JsonValueKind.Number && root.GetProperty(Stage).TryGetInt32(out var stage) &&
            stage == (boundary ? CompositionStage : InputStage), InvalidFrame);
        Assert.True(root.GetProperty(Failure).ValueKind == JsonValueKind.Number && root.GetProperty(Failure).TryGetInt32(out var failure) &&
            failure == (boundary ? ConfigurationFailure : RuntimeFailure), InvalidFrame);
        await child.RequireExitAsync(FailureExit);
    }
}
