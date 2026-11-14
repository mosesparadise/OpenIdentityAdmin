using System.Text.Json;
using IdentityPlatform.AppHost.Features.LocalDemo;

namespace IdentityPlatform.AppHost.Tests.Features.SampleConfiguration;

/// <summary>Observes only bounded synthetic setup stdin and models immutable Docker identity plus uncertain remote completion.</summary>
internal sealed class SampleProcessWitness : IDemoProcess
{
    internal const string Run = "1234567890abcdef1234567890abcdef";
    internal static readonly string Container = new('a', 64);
    private const string ClientRole = "sample-client";
    private const string Inspect = "inspect";
    private const string Stop = "stop";
    private const string ClientId = "clientId";
    private const string ClientSecret = "clientSecret";
    private const int InputFields = 2;
    private const int ExecTarget = 2;
    private const int StopTarget = 3;
    internal string Label { get; set; } = Run;
    internal string Role { get; set; } = ClientRole;
    internal string Id { get; set; } = Container;
    internal bool Running { get; set; } = true;
    internal bool FailMutation
    {
        get; set;
    }
    internal bool FailCleanup
    {
        get; set;
    }
    internal Func<CancellationToken, Task>? OnMutation
    {
        get; set;
    }
    internal int Mutations
    {
        get; private set;
    }
    internal int Stops
    {
        get; private set;
    }
    internal ReadOnlyMemory<byte> Submitted
    {
        get; private set;
    }

    /// <summary>Verifies actual argument isolation and records the original buffer to witness post-command zeroing.</summary>
    public async Task<string> RunAsync(IReadOnlyList<string> arguments, ReadOnlyMemory<byte> input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Assert.DoesNotContain(arguments, value => value.Contains(SampleInteraction.Secret, StringComparison.Ordinal));
        if (arguments[0] == Inspect)
        {
            return $"{Id}|{Label}|{Role}|{Running.ToString().ToLowerInvariant()}";
        }
        if (arguments[0] == Stop)
        {
            Assert.Equal(Container, arguments[StopTarget]);
            Stops++;
            if (FailCleanup)
            {
                throw new InvalidOperationException(SampleInteraction.Secret);
            }
            Running = false;
            return Container;
        }
        Assert.Equal(Container, arguments[ExecTarget]);
        Mutations++;
        Submitted = input;
        using (var json = JsonDocument.Parse(input))
        {
            Assert.Equal(SampleInteraction.User, json.RootElement.GetProperty(ClientId).GetString());
            Assert.Equal(SampleInteraction.Secret, json.RootElement.GetProperty(ClientSecret).GetString());
            Assert.Equal(InputFields, json.RootElement.EnumerateObject().Count());
        }
        if (OnMutation is not null)
        {
            await OnMutation(token);
        }
        if (FailMutation)
        {
            throw new InvalidOperationException(SampleInteraction.Secret);
        }
        return string.Empty;
    }
}
