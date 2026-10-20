using System.Diagnostics;

namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Selects only the real driver or fixed synthetic process controls; no arbitrary command can arrive through input protocol.</summary>
internal static class DriverProcessStart
{
    private const string DriverDirectory = "governance-driver";
    private const string Executable = "IdentityPlatform.Governance.Driver.dll";
    private const string NativeClientDirectory = "native-client-driver";
    private const string NativeClientExecutable = "IdentityPlatform.NativeClient.Driver.dll";
    private const string HostVariable = "DOTNET_HOST_PATH";
    private const string Dotnet = "dotnet";
    private const string Setsid = "setsid";
    private const string OptionEnd = "--";
    private const string Python = "python3";
    private const string Unbuffered = "-u";
    private const string ScriptArgument = "-c";
    private const string Stubborn = "stubborn";
    private const string LeaderExit = "leader-exit";
    private const string LinuxRequired = "Driver process containment requires Linux or WSL2.";
    private const string ControlScript = """
        import os, signal, sys
        if sys.stdin.buffer.readline() != b'start\n':
            os._exit(2)
        stubborn = sys.argv[1] == 'stubborn'
        if stubborn:
            signal.signal(signal.SIGTERM, signal.SIG_IGN)
        reader, writer = os.pipe()
        child = os.fork()
        if child == 0:
            os.close(reader)
            signal.signal(signal.SIGTERM, signal.SIG_IGN)
            null = os.open(os.devnull, os.O_WRONLY)
            os.dup2(null, 2)
            os.close(null)
            os.write(writer, b'1')
            os.close(writer)
            while True:
                signal.pause()
        os.close(writer)
        if os.read(reader, 1) != b'1':
            os._exit(2)
        os.close(reader)
        print('{"phase":"ready"}', flush=True)
        while True:
            command = sys.stdin.buffer.readline()
            if command == b'exit\n':
                os._exit(0)
            if command == b'alive\n':
                print('{"phase":"ready"}', flush=True)
            if command == b'':
                signal.pause()
        """;

    /// <summary>Launches a dedicated setsid group while retaining the complete isolated dependency directory for the actual driver.</summary>
    internal static ProcessStartInfo Create(GovernanceDriverControl mode, DriverExecutable executable = DriverExecutable.Governance)
    {
        Assert.True(OperatingSystem.IsLinux(), LinuxRequired);
        Assert.True(executable == DriverExecutable.Governance || mode == GovernanceDriverControl.None);
        var start = new ProcessStartInfo(Setsid)
        {
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        start.ArgumentList.Add(OptionEnd);
        if (mode is GovernanceDriverControl.LeaderExit or GovernanceDriverControl.Stubborn)
        {
            start.ArgumentList.Add(Python);
            start.ArgumentList.Add(Unbuffered);
            start.ArgumentList.Add(ScriptArgument);
            start.ArgumentList.Add(ControlScript);
            start.ArgumentList.Add(mode == GovernanceDriverControl.Stubborn ? Stubborn : LeaderExit);
        }
        else
        {
            Assert.True(mode == GovernanceDriverControl.None);
            var path = executable switch
            {
                DriverExecutable.Governance => Path.Combine(AppContext.BaseDirectory, DriverDirectory, Executable),
                DriverExecutable.NativeClient => Path.Combine(AppContext.BaseDirectory, NativeClientDirectory, NativeClientExecutable),
                _ => throw new InvalidOperationException()
            };
            Assert.True(File.Exists(path));
            start.ArgumentList.Add(Environment.GetEnvironmentVariable(HostVariable) ?? Dotnet);
            start.ArgumentList.Add(path);
        }
        return start;
    }
}

/// <summary>Names fixed test-only choices; no arbitrary program, shell statement or SQL is accepted.</summary>
internal enum GovernanceDriverControl
{
    None, LeaderExit, Stubborn
}

/// <summary>Restricts ordinary child startup to reviewed compiled test drivers; input frames cannot choose an executable.</summary>
internal enum DriverExecutable
{
    Governance, NativeClient
}
