using System.Diagnostics;
using System.Text;
using IdentityPlatform.AppHost.Features.LocalDemo;

namespace IdentityPlatform.AppHost.Tests.Features.DemoInitialization;

/// <summary>Exercises the real C# process adapter with controlled native children and synthetic input.</summary>
[Trait("Category", "Harness")]
public sealed class DemoProcessTests
{
    private const string AliveFailure = "Owned native process remained alive after command cleanup.";

    private const string WindowsPython = "python";
    private const string UnixPython = "python3";
    private const string ScriptArgument = "-c";
    private const string EchoMode = "echo";
    private const string SyntheticUnicode = "synthetic-λ-stdin";
    private const string BlockedMode = "blocked";
    private const string WitnessPrefix = "demo-process-";
    private const string GuidFormat = "N";
    private const string AbsentExecutable = "nonexistent-should-never-start";
    private const int WitnessSeconds = 5;
    private const int FailureSeconds = 2;
    private const int LongDeadlineSeconds = 30;
    private const int TimeoutSeconds = 3;
    private const int PollMilliseconds = 20;
    private const int MaximumInputBytes = 4096;
    private const int OversizedInputBytes = 4097;

    private const string Child = """
        import os, sys, time, subprocess
        mode = sys.argv[1]
        if mode == 'blocked':
            child = subprocess.Popen([sys.executable, '-c', 'import time; time.sleep(60)'])
            with open(sys.argv[2], 'w') as output:
                output.write(str(os.getpid()) + ',' + str(child.pid))
            time.sleep(60)
        data = sys.stdin.buffer.read()
        if mode == 'echo':
            assert not data.startswith(b'\xef\xbb\xbf')
            assert data.decode('utf-8') == 'synthetic-λ-stdin'
            assert 'synthetic-λ-stdin' not in sys.argv
            sys.stdout.write('stdin-and-eof-verified')
        if mode == 'failure':
            sys.stderr.write('Aa1!synthetic-canary-password')
            sys.exit(1)
        if mode == 'stdout-overflow':
            sys.stdout.write('x' * 20000)
        if mode == 'stderr-overflow':
            sys.stderr.write('y' * 20000)
        """;
    private static string Python => OperatingSystem.IsWindows() ? WindowsPython : UnixPython;

    /// <summary>The actual child observes exact Unicode bytes, no BOM, no secret argv and EOF.</summary>
    [Fact]
    public async Task Given_UnicodeInput_When_NativeProcessReadsStdin_Then_BytesAndEofArePreserved()
    {
        var process = new DemoProcess(Python, [ScriptArgument, Child], TimeSpan.FromSeconds(WitnessSeconds));
        var result = await process.RunAsync([EchoMode], Encoding.UTF8.GetBytes(SyntheticUnicode), TestContext.Current.CancellationToken);
        Assert.Equal("stdin-and-eof-verified", result);
    }

    /// <summary>Raw failure text and oversized pipes never escape as command diagnostics.</summary>
    [Theory]
    [InlineData("failure")]
    [InlineData("stdout-overflow")]
    [InlineData("stderr-overflow")]
    public async Task Given_FailingOrOverflowingProcess_When_Executed_Then_FiniteErrorContainsNoChildOutput(string mode)
    {
        var process = new DemoProcess(Python, [ScriptArgument, Child], TimeSpan.FromSeconds(FailureSeconds));
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync([mode], ReadOnlyMemory<byte>.Empty, TestContext.Current.CancellationToken));
        Assert.Equal("Local demo process did not complete.", error.Message);
        Assert.Null(error.InnerException);
    }

    /// <summary>Timeout and cancellation terminate the observed child and descendant, not only the caller's wait.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_BlockedNativeTree_When_ExecutionStops_Then_ObservedProcessesHaveExited(bool cancel)
    {
        var witness = Path.Combine(Path.GetTempPath(), WitnessPrefix + Guid.NewGuid().ToString(GuidFormat));
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        var process = new DemoProcess(Python, [ScriptArgument, Child], TimeSpan.FromSeconds(cancel ? LongDeadlineSeconds : TimeoutSeconds));
        var execution = process.RunAsync([BlockedMode, witness], new byte[MaximumInputBytes], cancellation.Token);
        try
        {
            await WaitForWitnessAsync(witness);
            var ids = (await File.ReadAllTextAsync(witness, TestContext.Current.CancellationToken)).Split(',').Select(int.Parse).ToArray();
            Assert.Equal(FailureSeconds, ids.Length);
            if (cancel)
            {
                await cancellation.CancelAsync();
            }
            await Assert.ThrowsAsync<InvalidOperationException>(() => execution);
            Assert.All(ids, id => Assert.True(HasExited(id), AliveFailure));
        }
        finally
        {
            await cancellation.CancelAsync();
            try
            {
                await execution;
            }
            catch (InvalidOperationException) { }
            if (File.Exists(witness))
            {
                foreach (var id in (await File.ReadAllTextAsync(witness, TestContext.Current.CancellationToken)).Split(',').Select(int.Parse))
                {
                    if (!HasExited(id))
                    {
                        using var orphan = Process.GetProcessById(id);
                        orphan.Kill(entireProcessTree: true);
                    }
                }
                File.Delete(witness);
            }
        }
    }

    /// <summary>Waits for the child-owned synthetic PID receipt before testing cancellation.</summary>
    private static async Task WaitForWitnessAsync(string path)
    {
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(WitnessSeconds));
        while (!File.Exists(path) || new FileInfo(path).Length == 0)
        {
            await Task.Delay(PollMilliseconds, deadline.Token);
        }
    }

    /// <summary>Accepts exited Windows processes or Linux zombies, which no longer execute and await their system reaper.</summary>
    private static bool HasExited(int id)
    {
        try
        {
            using var process = Process.GetProcessById(id);
            if (process.HasExited)
            {
                return true;
            }
            const string processRoot = "/proc/";
            const string statusFile = "/stat";
            var status = processRoot + id.ToString(System.Globalization.CultureInfo.InvariantCulture) + statusFile;
            return OperatingSystem.IsLinux() && File.Exists(status) && File.ReadAllText(status).Split(')')[1].TrimStart().StartsWith('Z');
        }
        catch (ArgumentException) { return true; }
        catch (FileNotFoundException) { return true; }
    }

    /// <summary>Oversized documents are denied before any executable is started.</summary>
    [Fact]
    public async Task Given_OversizedInput_When_Invoked_Then_NoProcessCanStart()
    {
        var process = new DemoProcess(AbsentExecutable);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => process.RunAsync([], new byte[OversizedInputBytes], TestContext.Current.CancellationToken));
        Assert.Equal("Local demo process did not complete.", error.Message);
    }
}
