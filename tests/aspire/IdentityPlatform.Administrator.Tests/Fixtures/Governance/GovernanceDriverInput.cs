using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Core.Features.Identity.Governance;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Writes bounded synthetic credentials directly to the owned child's pipe, never arguments, environment or disk.</summary>
internal static class GovernanceDriverInput
{
    private const int FrameLimit = 65_536;
    private const string ReadOperation = "Read";
    private const string ExecuteOperation = "Execute";
    private const string InputTooLarge = "Synthetic driver input exceeded its reviewed transport bound.";
    private static readonly byte[] Newline = "\n"u8.ToArray();

    /// <summary>Sends only finite immutable intent and untrusted session identifiers; real child services must establish every capability.</summary>
    internal static Task ExecuteAsync(Stream pipe, GovernanceScenario scenario, string provider, GovernanceRequest request,
        JourneySessionCandidate candidate, string password, string code, bool waitForAcknowledgement, CancellationToken token) =>
        WriteAsync(pipe, new
        {
            Operation = ExecuteOperation,
            Provider = provider,
            scenario.Journey.Lease.RuntimeConnection,
            scenario.Journey.IssuerSettings,
            Command = new
            {
                Action = request.Action.ToString(),
                request.Versions,
                request.UserId,
                request.RoleId,
                request.Permission
            },
            Candidate = new
            {
                candidate.UserId,
                candidate.SessionId
            },
            Password = password,
            Code = code,
            AwaitAcknowledgement = waitForAcknowledgement
        }, token);

    /// <summary>Requests a fresh-node observation without replaying a command after a lost result.</summary>
    internal static Task ReadAsync(Stream pipe, GovernanceScenario scenario, string provider, CancellationToken token) =>
        WriteAsync(pipe, new
        {
            Operation = ReadOperation,
            Provider = provider,
            scenario.Journey.Lease.RuntimeConnection,
            scenario.Journey.IssuerSettings,
            scenario.ActorId
        }, token);

    /// <summary>Zeroes the temporary input buffer after a bounded write, preserving pipe ownership for an optional acknowledgement.</summary>
    private static async Task WriteAsync<T>(Stream pipe, T input, CancellationToken token)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(input);
        try
        {
            Assert.True(bytes.Length < FrameLimit, InputTooLarge);
            await pipe.WriteAsync(bytes, token);
            await pipe.WriteAsync(Newline, token);
            await pipe.FlushAsync(token);
        }
        finally { CryptographicOperations.ZeroMemory(bytes); }
    }
}
