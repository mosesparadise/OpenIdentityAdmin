using System.Globalization;
using IdentityPlatform.Testing;

namespace IdentityPlatform.Issuer.Lifecycle.Tests.Fixtures;

internal static class ProcessListener
{
    internal static int Capture(ProcessGeneration process)
    {
        Assert.False(ProcessTreeEvidence.Terminated(process), "Issuer generation ended before listener capture.");
        var sockets = Directory.EnumerateFiles($"/proc/{process.Id}/fd").Select(path => new FileInfo(path).LinkTarget)
            .Where(value => value is not null && value.StartsWith("socket:[", StringComparison.Ordinal) && value.EndsWith(']'))
            .Select(value => value![8..^1]).ToHashSet(StringComparer.Ordinal);
        var rows = File.ReadLines($"/proc/{process.Id}/net/tcp").Skip(1)
            .Concat(File.ReadLines($"/proc/{process.Id}/net/tcp6").Skip(1));
        var port = RequirePort(rows, sockets);
        Assert.False(ProcessTreeEvidence.Terminated(process), "Issuer generation changed during listener capture.");
        return port;
    }

    internal static int RequirePort(IEnumerable<string> rows, HashSet<string> sockets)
    {
        var ports = new HashSet<int>();
        foreach (var row in rows)
        {
            var fields = row.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            Assert.True(fields.Length >= 10, "Malformed TCP evidence.");
            if (fields[3] != "0A" || !sockets.Contains(fields[9]))
            {
                continue;
            }
            var address = fields[1].Split(':');
            Assert.True(address.Length == 2 && address[0] is "0100007F" or "00000000000000000000000001000000", "Issuer listener is not loopback-only.");
            var port = int.Parse(address[1], NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            Assert.True(port is > 0 and <= 65535, "Invalid listener port.");
            ports.Add(port);
        }
        Assert.True(ports.Count == 1, "Expected one process-owned application listener port.");
        return ports.Single();
    }
}
