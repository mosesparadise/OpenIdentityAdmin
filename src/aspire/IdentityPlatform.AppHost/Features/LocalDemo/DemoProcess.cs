using System.Diagnostics;
using System.Text;

namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Owns the bounded local Docker transport without forwarding process diagnostics.</summary>
internal interface IDemoProcess
{
    /// <summary>Sends bounded UTF-8 bytes followed by EOF and returns bounded captured output only to its owner.</summary>
    Task<string> RunAsync(IReadOnlyList<string> arguments, ReadOnlyMemory<byte> input, CancellationToken token);
}

/// <summary>Drains both pipes concurrently and joins process/I/O ownership on cancellation or timeout.</summary>
internal sealed class DemoProcess(string executable = "docker", string[]? prefix = null, TimeSpan? timeout = null) : IDemoProcess
{
    private const int OutputLimit = 16384;
    private const int BufferLength = 1024;
    private const int InputLimit = 4096;
    private const string Failure = "Local demo process did not complete.";
    private static readonly TimeSpan OperationTimeout = TimeSpan.FromSeconds(90);
    private static readonly TimeSpan CleanupTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Uses argument lists and raw stdin so passwords never enter argv, files or resource logs.</summary>
    public async Task<string> RunAsync(IReadOnlyList<string> arguments, ReadOnlyMemory<byte> input, CancellationToken token)
    {
        if (input.Length > InputLimit)
        {
            throw new InvalidOperationException(Failure);
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(token);
        deadline.CancelAfter(timeout ?? OperationTimeout);
        using var process = new Process { StartInfo = CreateStartInfo(arguments) };
        Task[] operations = [];
        try
        {
            deadline.Token.ThrowIfCancellationRequested();
            if (!process.Start())
            {
                throw new InvalidOperationException(Failure);
            }
            var output = ReadAsync(process.StandardOutput, deadline.Token);
            operations = [output, ReadAsync(process.StandardError, deadline.Token), WriteAsync(process, input, deadline.Token), process.WaitForExitAsync(deadline.Token)];
            await Task.WhenAll(operations).WaitAsync(deadline.Token);
            if (process.ExitCode != 0)
            {
                throw new InvalidOperationException(Failure);
            }
            return await output;
        }
        catch (Exception) { throw new InvalidOperationException(Failure); }
        finally
        {
            await JoinAsync(process, operations, deadline);
        }
    }

    /// <summary>Creates an isolated native invocation without shell expansion or inherited console streams.</summary>
    private ProcessStartInfo CreateStartInfo(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo(executable)
        {
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8
        };
        foreach (var value in (prefix ?? []).Concat(arguments))
        {
            start.ArgumentList.Add(value);
        }
        return start;
    }

    /// <summary>Bounds memory while draining the pipe, rejecting overflow without including its contents.</summary>
    private static async Task<string> ReadAsync(StreamReader reader, CancellationToken token)
    {
        var buffer = new char[BufferLength];
        var output = new StringBuilder();
        try
        {
            int count;
            while ((count = await reader.ReadAsync(buffer.AsMemory(), token)) != 0)
            {
                if (output.Length + count > OutputLimit)
                {
                    throw new InvalidOperationException(Failure);
                }
                output.Append(buffer, 0, count);
            }
            return output.ToString();
        }
        finally { Array.Clear(buffer); output.Clear(); }
    }

    /// <summary>Closes stdin after the exact bytes, including empty input; never adds a BOM or a newline.</summary>
    private static async Task WriteAsync(Process process, ReadOnlyMemory<byte> input, CancellationToken token)
    {
        try
        {
            await process.StandardInput.BaseStream.WriteAsync(input, token);
            await process.StandardInput.BaseStream.FlushAsync(token);
        }
        finally { process.StandardInput.Close(); }
    }

    /// <summary>Terminates a remaining local process tree and bounds joining all pipe tasks on every exit path.</summary>
    private static async Task JoinAsync(Process process, Task[] operations, CancellationTokenSource deadline)
    {
        await deadline.CancelAsync();
        using var cleanup = new CancellationTokenSource(CleanupTimeout);
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
            await process.WaitForExitAsync(cleanup.Token);
            try
            {
                await Task.WhenAll(operations).WaitAsync(cleanup.Token);
            }
            catch (Exception) when (operations.All(operation => operation.IsCompleted)) { }
        }
        catch (InvalidOperationException) when (operations.Length == 0) { }
        catch (Exception) { throw new InvalidOperationException(Failure); }
    }
}
