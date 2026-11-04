using System.Diagnostics;
using System.Globalization;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Owns one real X11 browser window on the lifecycle lane's private display, without synthetic DOM visibility events.</summary>
internal sealed class ApplicationNativeWindow(string id)
{
    private const string Tool = "xdotool";
    private const string Properties = "xprop";
    private const string Search = "search";
    private const string Sync = "--sync";
    private const string OnlyVisible = "--onlyvisible";
    private const string Name = "--name";
    private const string IdentifierFormat = "N";
    private const string Minimize = "windowminimize";
    private const string Map = "windowmap";
    private const string Activate = "windowactivate";
    private const string Identifier = "-id";
    private const string State = "WM_STATE";
    private const string MarkerPrefix = "identity-native-visibility-";
    private const string Title = "title => { const original=document.title; document.title=title; return original; }";
    private const string Iconic = "Iconic";
    private const string Normal = "Normal";
    private const int Limit = 1024;
    private static readonly TimeSpan Budget = TimeSpan.FromSeconds(3);

    /// <summary>Temporarily labels the actual page, requires one visible native window, then restores the original document title privately.</summary>
    internal static async Task<ApplicationNativeWindow> FindAsync(IPage page)
    {
        Assert.True(OperatingSystem.IsLinux());
        var marker = MarkerPrefix + Guid.NewGuid().ToString(IdentifierFormat);
        var original = await page.EvaluateAsync<string>(Title, marker);
        try
        {
            var result = await CommandAsync(Tool, Search, Sync, OnlyVisible, Name, marker);
            var ids = result.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            Assert.True(ids.Length == 1 && ulong.TryParse(ids[0], NumberStyles.None, CultureInfo.InvariantCulture, out var value) && value != 0);
            return new(ids[0]);
        }
        finally { await page.EvaluateAsync<string>(Title, original); }
    }

    /// <summary>Asks the window manager to minimize the identified native window and requires its real iconic state.</summary>
    internal async Task MinimizeAsync()
    {
        await CommandAsync(Tool, Minimize, Sync, id);
        var state = await CommandAsync(Properties, Identifier, id, State);
        Assert.True(state.Contains(Iconic, StringComparison.Ordinal));
    }

    /// <summary>Maps and activates the same owned window; it never substitutes another page or supplies a visibility value.</summary>
    internal async Task RestoreAsync()
    {
        await CommandAsync(Tool, Map, Sync, id);
        await CommandAsync(Tool, Activate, Sync, id);
        var state = await CommandAsync(Properties, Identifier, id, State);
        Assert.True(state.Contains(Normal, StringComparison.Ordinal));
    }

    /// <summary>Bounds fixed native utilities and their output, joins failures, and never publishes arbitrary process diagnostics.</summary>
    private static async Task<string> CommandAsync(string command, params string[] arguments)
    {
        using var process = new Process { StartInfo = new(command) { UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true } };
        foreach (var argument in arguments)
        {
            process.StartInfo.ArgumentList.Add(argument);
        }
        Assert.True(process.Start());
        var output = ReadAsync(process.StandardOutput);
        var error = ReadAsync(process.StandardError);
        var exit = process.WaitForExitAsync(TestContext.Current.CancellationToken);
        var joined = Task.WhenAll(exit, output, error);
        try
        {
            await joined.WaitAsync(Budget, TestContext.Current.CancellationToken);
            Assert.Equal(0, process.ExitCode);
            return await output;
        }
        catch (Exception failure)
        {
            try
            {
                if (!process.HasExited)
                {
                    process.Kill(entireProcessTree: true);
                }
                await Task.WhenAll(process.WaitForExitAsync(CancellationToken.None), output, error).WaitAsync(Budget);
            }
            catch (Exception cleanup) { throw new AggregateException(failure, cleanup); }
            throw;
        }
    }

    /// <summary>Retains only a small private process result and rejects any unexpected excess instead of logging it.</summary>
    private static async Task<string> ReadAsync(StreamReader reader)
    {
        var buffer = new char[Limit];
        var length = await reader.ReadBlockAsync(buffer, TestContext.Current.CancellationToken);
        Assert.True(length < Limit);
        return new(buffer, 0, length);
    }
}
