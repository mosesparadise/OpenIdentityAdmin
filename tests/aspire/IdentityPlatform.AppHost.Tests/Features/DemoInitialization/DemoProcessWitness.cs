using System.Text;
using System.Text.Json;
using IdentityPlatform.AppHost.Features.LocalDemo;

namespace IdentityPlatform.AppHost.Tests.Features.DemoInitialization;

/// <summary>Observes synthetic stdin mutations and immutable ownership without retaining password documents.</summary>
internal sealed class DemoProcessWitness : IDemoProcess
{
    private static readonly string[] BootstrapFields = ["operationId", "operatorReference", "password", "userName"];
    private const string OperatorField = "operatorReference";
    private const string ExpectedOperator = "local-demo";
    private const string InitializerRole = "initializer";
    private const string Inspect = "inspect";
    private const string Exec = "exec";
    private const string Stop = "stop";
    private const string UsernameField = "userName";
    private const string PasswordField = "password";
    private const string OperationField = "operationId";
    private const string RegistrationScript = "/app/register.sh";
    private const int ExecTargetIndex = 2;
    private const int StopTargetIndex = 3;

    internal const string Run = "1234567890abcdef1234567890abcdef";
    internal static readonly string Container = new('a', 64);
    internal string Label { get; set; } = Run;
    internal string Role { get; set; } = InitializerRole;
    internal string Id { get; set; } = Container;
    internal bool Running { get; set; } = true;
    internal int Mutations
    {
        get; private set;
    }
    internal int Stops
    {
        get; private set;
    }
    internal int FailAt
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
    internal List<string[]> Arguments { get; } = [];
    internal bool ValidBootstrap
    {
        get; private set;
    }

    /// <summary>Checks the bootstrap document independently and models bounded safe metadata or a private native failure.</summary>
    public async Task<string> RunAsync(IReadOnlyList<string> arguments, ReadOnlyMemory<byte> input, CancellationToken token)
    {
        token.ThrowIfCancellationRequested();
        Arguments.Add(arguments.ToArray());
        var exposed = arguments.Any(value => value.Contains(DemoInteraction.Secret, StringComparison.Ordinal));
        Assert.False(exposed);
        if (arguments[0] == Inspect)
        {
            return $"{Id}|{Label}|{Role}|{Running.ToString().ToLowerInvariant()}\n";
        }
        Assert.Equal(Container, arguments[arguments[0] == Exec ? ExecTargetIndex : StopTargetIndex]);
        if (arguments[0] == Stop)
        {
            Stops++;
            if (FailCleanup)
            {
                throw new InvalidOperationException(DemoInteraction.Secret);
            }
            Running = false;
            return Container;
        }
        Mutations++;
        if (OnMutation is not null)
        {
            await OnMutation(token);
        }
        if (Mutations == FailAt)
        {
            throw new InvalidOperationException(DemoInteraction.Secret);
        }
        if (Mutations == 1)
        {
            Assert.False(input.Span.StartsWith(Encoding.UTF8.Preamble));
            using var json = JsonDocument.Parse(input);
            Assert.Equal(BootstrapFields, json.RootElement.EnumerateObject().Select(field => field.Name).Order(StringComparer.Ordinal));
            Assert.True(json.RootElement.GetProperty(OperatorField).GetString() == ExpectedOperator);
            ValidBootstrap = json.RootElement.GetProperty(UsernameField).GetString() == DemoInteraction.User &&
                json.RootElement.GetProperty(PasswordField).GetString() == DemoInteraction.Secret;
            Assert.True(Guid.TryParse(json.RootElement.GetProperty(OperationField).GetString(), out _));
        }
        else
        {
            Assert.Empty(input.ToArray());
            Assert.Equal(RegistrationScript, arguments[StopTargetIndex]);
        }
        return string.Empty;
    }
}
