namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Binds stdin mutations to an immutable, inspected container ID owned by this exact run.</summary>
internal sealed class DemoInitializer(IDemoProcess process, string run)
{
    private const string Exec = "exec";
    private const string Interactive = "-i";
    private const string Dotnet = "dotnet";
    private const string Migrator = "/app/IdentityPlatform.Migrator.dll";
    private const string OperationArgument = "--Operation";
    private const string Bootstrap = "initialize-administrator";
    private const string RegistrationScript = "/app/register.sh";
    private const string Stop = "stop";
    private const string StopTimeoutArgument = "--time";
    private const string StopGraceSeconds = "5";
    private const string Inspect = "inspect";
    private const string Format = "--format";

    private const string ContainerPrefix = "oid-demo-";
    private const string InitializerSuffix = "-initializer";
    private const string Role = "initializer";
    private const string Projection = "{{.Id}}|{{index .Config.Labels \"openidentityadmin.local-demo.run\"}}|{{index .Config.Labels \"openidentityadmin.local-demo.role\"}}|{{.State.Running}}";
    private const string Running = "true";
    private const string Stopped = "false";
    private const string Failure = "Local demo initializer ownership or state is invalid.";
    private const int ContainerIdLength = 64;
    private const int FieldCount = 4;
    private const int IdField = 0;
    private const int RunField = 1;
    private const int RoleField = 2;
    private const int StateField = 3;
    private const int RunLength = 32;
    private string? container;
    internal bool Attempted
    {
        get; private set;
    }

    /// <summary>Verifies only non-secret Docker identity fields and captures the immutable target ID.</summary>
    internal async Task BindAsync(CancellationToken token)
    {
        if (run.Length != RunLength || !run.All(IsHex))
        {
            throw new InvalidOperationException(Failure);
        }
        var fields = await InspectAsync(ContainerPrefix + run + InitializerSuffix, Running, token);
        container = fields[IdField];
    }

    /// <summary>Rechecks readiness before sending the bounded bootstrap and public-only registration sequence.</summary>
    internal async Task InitializeAsync(ReadOnlyMemory<byte> bootstrap, CancellationToken token)
    {
        var target = container ?? throw new InvalidOperationException(Failure);
        await InspectAsync(target, Running, token);
        token.ThrowIfCancellationRequested();
        Attempted = true;
        await process.RunAsync([Exec, Interactive, target, Dotnet, Migrator, OperationArgument, Bootstrap], bootstrap, token);
        await InspectAsync(target, Running, token);
        await process.RunAsync([Exec, Interactive, target, RegistrationScript], ReadOnlyMemory<byte>.Empty, token);
    }

    /// <summary>Stops only the pinned, owned target after uncertain mutation and proves it no longer runs.</summary>
    internal async Task StopAsync(CancellationToken token)
    {
        var target = container ?? throw new InvalidOperationException(Failure);
        await InspectAsync(target, null, token);
        await process.RunAsync([Stop, StopTimeoutArgument, StopGraceSeconds, target], ReadOnlyMemory<byte>.Empty, token);
        await InspectAsync(target, Stopped, token);
    }

    /// <summary>Rejects a replaced container, foreign label, wrong role or unexpected execution state.</summary>
    private async Task<string[]> InspectAsync(string target, string? state, CancellationToken token)
    {
        var output = await process.RunAsync([Inspect, Format, Projection, target], ReadOnlyMemory<byte>.Empty, token);
        var fields = output.Trim().Split('|');
        if (fields.Length != FieldCount || fields[IdField].Length != ContainerIdLength || !fields[IdField].All(IsHex) ||
            fields[RunField] != run || fields[RoleField] != Role || (state is not null && fields[StateField] != state) ||
            (container is not null && fields[IdField] != container))
        {
            throw new InvalidOperationException(Failure);
        }
        return fields;
    }

    /// <summary>Accepts only the canonical Docker/run identity alphabet, never shell metacharacters.</summary>
    private static bool IsHex(char value) => char.IsAsciiDigit(value) || value is >= 'a' and <= 'f';
}
