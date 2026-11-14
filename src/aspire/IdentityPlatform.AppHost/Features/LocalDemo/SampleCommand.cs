using System.Security.Cryptography;
using System.Text.Json;

namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Transfers manually issued sample credentials only through authenticated dashboard input and a pinned run-owned Docker stdin target.</summary>
internal sealed class SampleCommand(IDemoProcess process, string run)
{
    private const int InteractionMinutes = 5;
    private const string ContainerNamePrefix = "oid-demo-";
    private const string NameSeparator = "-";
    private const string SetupTitle = "Configure sample client";
    private const string SetupPrompt = "Create the application and scopes in the administrator UI first. Enter the newly issued client secret for this disposable run.";
    private const string ExecCommand = "exec";
    private const string InteractiveArgument = "-i";
    private const string DotnetCommand = "dotnet";
    private const string ClientAssembly = "/app/IdentityPlatform.Sample.Client.dll";
    private const string ConfigureArgument = "--configure";
    private const string CompletedMessage = "Sample configured. Open https://sample.dev.localhost:17446/ to sign in.";
    private const int TerminalState = 2;
    private const string ClientIdField = "clientId";
    private const string ManagedClientIDText = "Managed client ID";
    private const int ClientIdLimit = 100;
    private const string ClientSecretField = "clientSecret";
    private const string OneTimeClientSecretText = "One-time client secret";
    private const int SecretLimit = 256;
    private const string TrueValue = "true";
    private const int RunLength = 32;
    private const string InspectCommand = "inspect";
    private const string FormatArgument = "--format";
    private const int ProjectionFieldCount = 4;
    private const int ContainerIdLength = 64;
    private const int StateField = 3;
    private const int RoleField = 2;
    private const int CleanupSeconds = 20;
    private const string StopCommand = "stop";
    private const string StopTimeoutArgument = "--time";
    private const string StopGraceSeconds = "5";
    private const string FalseValue = "false";
    private const string CleanupFailureMessage = "Sample setup did not complete and cleanup could not be confirmed. Stop the AppHost and inspect its owned resources before starting a fresh run.";

    private const string Role = "sample-client";
    private const string Failure = "Sample setup did not complete. If submission began, start a fresh disposable run; no operation was retried.";
    private const string Projection = "{{.Id}}|{{index .Config.Labels \"openidentityadmin.local-demo.run\"}}|{{index .Config.Labels \"openidentityadmin.local-demo.role\"}}|{{.State.Running}}";
    private int state;
    internal bool Available => Volatile.Read(ref state) == 0;

    /// <summary>Serializes one operator interaction, rechecks ownership and clears secret input regardless of outcome.</summary>
    internal async Task<ExecuteCommandResult> ExecuteAsync(IInteractionService interaction, Func<bool> running, CancellationToken token, Func<Task>? publishState = null)
    {
        if (Interlocked.CompareExchange(ref state, 1, 0) != 0)
        {
            return new ExecuteCommandResult { Success = false, Message = Failure };
        }
        var fields = Fields();
        InteractionInputCollection? returned = null;
        byte[] bytes = [];
        var submitted = false;
        string? target = null;
        try
        {
            if (publishState is not null)
            {
                await publishState();
            }
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
            deadline.CancelAfter(TimeSpan.FromMinutes(InteractionMinutes));
            if (!interaction.IsAvailable || !running())
            {
                return new ExecuteCommandResult { Success = false, Message = Failure };
            }
            target = await BindAsync(ContainerNamePrefix + run + NameSeparator + Role, deadline.Token);
            var response = await interaction.PromptInputsAsync(SetupTitle, SetupPrompt, fields, cancellationToken: deadline.Token);
            if (response.Canceled)
            {
                return new ExecuteCommandResult { Success = false, Canceled = true };
            }
            returned = response.Data;
            bytes = Encode(returned);
            if (!running() || await BindAsync(target, deadline.Token) != target)
            {
                return new ExecuteCommandResult { Success = false, Message = Failure };
            }
            deadline.Token.ThrowIfCancellationRequested();
            submitted = true;
            await process.RunAsync([ExecCommand, InteractiveArgument, target, DotnetCommand, ClientAssembly, ConfigureArgument], bytes, deadline.Token);
            return new ExecuteCommandResult { Success = true, Message = CompletedMessage };
        }
        catch (Exception)
        {
            var message = submitted && target is not null ? await StopUncertainAsync(target) : Failure;
            return new ExecuteCommandResult { Success = false, Message = message };
        }
        finally
        {
            CryptographicOperations.ZeroMemory(bytes);
            foreach (var input in fields)
            {
                input.Value = null;
            }
            if (returned is not null)
            {
                foreach (var input in returned)
                {
                    input.Value = null;
                }
            }
            Volatile.Write(ref state, submitted ? TerminalState : 0);
            if (publishState is not null)
            {
                await publishState();
            }
        }
    }

    /// <summary>Creates secret-aware dashboard fields without storing their values in the Aspire resource model.</summary>
    private static InteractionInput[] Fields() =>
    [
        new() { Name = ClientIdField, Label = ManagedClientIDText, InputType = InputType.Text, Required = true, MaxLength = ClientIdLimit },
        new() { Name = ClientSecretField, Label = OneTimeClientSecretText, InputType = InputType.SecretText, Required = true, MaxLength = SecretLimit }
    ];

    /// <summary>Revalidates and bounds operator data before it crosses stdin; destination identities never come from input.</summary>
    private static byte[] Encode(InteractionInputCollection values)
    {
        var id = values[ClientIdField].Value;
        var secret = values[ClientSecretField].Value;
        if (id is null || id.Length is 0 or > ClientIdLimit || !id.All(value => char.IsAsciiLetterOrDigit(value) || value is '-' or '_' or '.') ||
            secret is null || secret.Length is 0 or > SecretLimit || secret.Any(char.IsControl))
        {
            throw new InvalidOperationException(Failure);
        }
        return JsonSerializer.SerializeToUtf8Bytes(new
        {
            clientId = id,
            clientSecret = secret
        });
    }

    /// <summary>Accepts only the exact running container's immutable ID and current run/role labels.</summary>
    private async Task<string> BindAsync(string target, CancellationToken token, string? expectedState = TrueValue)
    {
        if (run.Length != RunLength || !run.All(IsHex))
        {
            throw new InvalidOperationException(Failure);
        }
        var output = await process.RunAsync([InspectCommand, FormatArgument, Projection, target], ReadOnlyMemory<byte>.Empty, token);
        var parts = output.Trim().Split('|');
        if (parts.Length != ProjectionFieldCount || parts[0].Length != ContainerIdLength || !parts[0].All(IsHex) || parts[1] != run ||
            parts[RoleField] != Role || (expectedState is not null && parts[StateField] != expectedState) ||
            (target.Length == ContainerIdLength && parts[0] != target))
        {
            throw new InvalidOperationException(Failure);
        }
        return parts[0];
    }

    /// <summary>Stops only the pinned owned client with a fresh budget after uncertain exec; late setup cannot survive in a running target.</summary>
    private async Task<string> StopUncertainAsync(string target)
    {
        using var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(CleanupSeconds));
        try
        {
            await BindAsync(target, cleanup.Token, expectedState: null);
            await process.RunAsync([StopCommand, StopTimeoutArgument, StopGraceSeconds, target], ReadOnlyMemory<byte>.Empty, cleanup.Token);
            await BindAsync(target, cleanup.Token, expectedState: FalseValue);
            return Failure;
        }
        catch (Exception) { return CleanupFailureMessage; }
    }

    /// <summary>Requires canonical run/container identities without shell syntax or ambiguous normalization.</summary>
    private static bool IsHex(char value) => char.IsAsciiDigit(value) || value is >= 'a' and <= 'f';
}
