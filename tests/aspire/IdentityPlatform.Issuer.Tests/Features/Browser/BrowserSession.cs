using System.Diagnostics;
using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal sealed class BrowserSession : IAsyncDisposable
{
    private readonly IPlaywright playwright;
    private IBrowser? browser;
    private string? profile;
    private BrowserSession(IPlaywright playwright) => this.playwright = playwright;
    public IBrowserContext Context { get; private set; } = null!;
    public static string Engine => SelectEngine(Environment.GetEnvironmentVariable("IDENTITY_TEST_BROWSER"));
    internal static string SelectEngine(string? value) => value switch
    {
        null or "chromium" => "chromium",
        "firefox" => "firefox",
        "webkit" => "webkit",
        _ => throw new InvalidOperationException("Unknown test browser; engine fallback is forbidden.")
    };

    public static async Task<BrowserSession> CreateAsync(int width = 1280, bool javaScript = true)
    {
        BrowserEvidence.Mark(BrowserPhase.Creation);
        var session = new BrowserSession(await Playwright.CreateAsync());
        try
        {
            if (Engine == "firefox")
            {
                session.profile = Path.Combine(Path.GetTempPath(), "identity-firefox-" + Guid.NewGuid().ToString("N"));
                if (OperatingSystem.IsWindows())
                {
                    Directory.CreateDirectory(session.profile);
                }
                else
                {
                    Directory.CreateDirectory(session.profile, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
                }
                var certificate = Environment.GetEnvironmentVariable("IDENTITY_TEST_CERTIFICATE");
                if (string.IsNullOrEmpty(certificate) || !File.Exists(certificate))
                {
                    throw new InvalidOperationException("Firefox requires the explicitly provisioned public test certificate.");
                }
                await ImportAsync(["-N", "-d", "sql:" + session.profile, "--empty-password"]);
                // Firefox certverifier requires an explicit trust anchor; peer trust alone is not honored.
                // This exact synthetic leaf is trusted only inside this disposable private profile.
                await ImportAsync(["-A", "-d", "sql:" + session.profile, "-n", "identityplatform-development", "-t", "C,,", "-i", certificate]);
                session.Context = await session.playwright.Firefox.LaunchPersistentContextAsync(session.profile, new()
                {
                    Headless = true,
                    ViewportSize = new()
                    {
                        Width = width,
                        Height = 900
                    },
                    ReducedMotion = ReducedMotion.Reduce,
                    JavaScriptEnabled = javaScript
                });
            }
            else
            {
                session.browser = await (Engine == "webkit" ? session.playwright.Webkit : session.playwright.Chromium).LaunchAsync();
                session.Context = await session.browser.NewContextAsync(new()
                {
                    ViewportSize = new()
                    {
                        Width = width,
                        Height = 900
                    },
                    ReducedMotion = ReducedMotion.Reduce,
                    JavaScriptEnabled = javaScript
                });
            }
            session.Context.SetDefaultTimeout(15_000);
            session.Context.SetDefaultNavigationTimeout(20_000);
            BrowserEvidence.Mark(BrowserPhase.Ready);
            return session;
        }
        catch
        {
            await session.DisposeAsync();
            throw;
        }
    }

    private static async Task ImportAsync(string[] arguments)
    {
        var start = new ProcessStartInfo("certutil") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Certificate import could not start.");
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(10));
        try
        {
            await process.WaitForExitAsync(deadline.Token);
            Assert.True(process.ExitCode == 0, "Firefox certificate import failed.");
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync(CancellationToken.None);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            if (Context is not null)
            {
                await Context.DisposeAsync();
            }
        }
        finally
        {
            try
            {
                if (browser is not null)
                {
                    await browser.DisposeAsync();
                }
            }
            finally
            {
                playwright.Dispose();
                if (profile is not null)
                {
                    Directory.Delete(profile, recursive: true);
                }
            }
        }
    }
}
