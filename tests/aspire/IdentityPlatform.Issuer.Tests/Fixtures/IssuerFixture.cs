using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using IdentityPlatform.AppHost.Tests.Features;
using IdentityPlatform.AuthServer.Features.Pilot;
using IdentityPlatform.AuthServer.Features.Protocol.Policy;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Features.Identity.Persistence;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Testing;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace IdentityPlatform.Issuer.Tests.Fixtures;

[CollectionDefinition("issuer")]
public sealed class IssuerTestGroup : ICollectionFixture<IssuerFixture>;

public sealed class IssuerFixture : IAsyncLifetime
{
    private readonly Dictionary<string, IssuerEnvironment> environments = new(StringComparer.Ordinal);
    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async Task<IssuerEnvironment> GetAsync(string provider)
    {
        if (environments.TryGetValue(provider, out var existing))
        {
            Assert.False(existing.Invalidated, "Earlier state restoration failed; refusing to reuse the issuer.");
            return existing;
        }
        var environment = await IssuerEnvironment.CreateAsync(provider, TestContext.Current.CancellationToken);
        environments.Add(provider, environment);
        return environment;
    }

    public async ValueTask DisposeAsync()
    {
        var failed = false;
        foreach (var environment in environments.Values)
        {
            try
            {
                await environment.DisposeAsync();
            }
            catch (Exception) { failed = true; }
        }
        Assert.False(failed, "One or more issuer environments failed cleanup.");
    }
}

public sealed partial class IssuerEnvironment : IAsyncDisposable
{
    private readonly IssuerMaterialFixture credentials = new();
    private DistributedApplication? application;
    private IDistributedApplicationTestingBuilder? builder;
    private ServiceProvider? services;
    private TestRelyingParty? relyingParty;
    private LifecycleEvidence? lifecycle;
    public string Password { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
    public string ClientSecret { get; } = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
    public string Username { get; } = "pilot.user";
    public Uri Issuer { get; private set; } = null!;
    public TestRelyingParty RelyingParty => relyingParty!;
    public IServiceProvider Services => services!;
    public DistributedApplication Application => application!;
    public bool Invalidated
    {
        get; private set;
    }
    public void Invalidate() => Invalidated = true;

    public static async Task<IssuerEnvironment> CreateAsync(string provider, CancellationToken cancellationToken)
    {
        var environment = new IssuerEnvironment();
        try
        {
            await environment.StartAsync(provider, cancellationToken);
            return environment;
        }
        catch
        {
            try
            {
                await environment.DisposeAsync();
            }
            catch (Exception) { environment.Invalidate(); }
            throw;
        }
    }

    private async Task StartAsync(string provider, CancellationToken cancellationToken)
    {
        using var startup = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        startup.CancelAfter(TimeSpan.FromMinutes(3));
        relyingParty = new TestRelyingParty { ClientSecret = ClientSecret };
        await relyingParty.StartAsync(startup.Token);
        builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.IdentityPlatform_AppHost>(
            ["--environment=Development", $"--Database:Provider={provider}", "--Pilot:Enabled=true"], startup.Token);
        var auth = builder.CreateResourceBuilder<ProjectResource>("auth");
        var bootstrap = builder.CreateResourceBuilder<ProjectResource>("pilot-bootstrap");
        foreach (var setting in credentials.Settings.Where(setting => setting.Key != "Identity:Issuer"))
        {
            var variable = setting.Key.Replace(":", "__");
            if (setting.Key.EndsWith(":Password", StringComparison.Ordinal))
            {
                var secret = builder.AddParameter("fixture-" + setting.Key.Replace(":", "-").ToLowerInvariant(), () => setting.Value!, secret: true);
                auth.WithEnvironment(variable, secret);
                bootstrap.WithEnvironment(variable, secret);
            }
            else
            {
                auth.WithEnvironment(variable, setting.Value!);
                bootstrap.WithEnvironment(variable, setting.Value!);
            }
        }
        bootstrap.WithEnvironment("Pilot__UserName", Username)
            .WithEnvironment("Pilot__RedirectUri", RelyingParty.Callback)
            .WithEnvironment("Pilot__Password", builder.AddParameter("fixture-user-password", () => Password, secret: true))
            .WithEnvironment("Pilot__ClientSecret", builder.AddParameter("fixture-client-secret", () => ClientSecret, secret: true));
        application = await builder.BuildAsync(startup.Token);
        await application.StartAsync(startup.Token);
        await application.ResourceNotifications.WaitForResourceHealthyAsync("auth", startup.Token);
        lifecycle = LifecycleEvidence.Capture(application);
        Issuer = application.GetEndpoint("auth", "https");
        relyingParty.Issuer = Issuer;
        var connection = await application.GetConnectionStringAsync("platform", startup.Token);
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Pilot:UserName"] = Username,
            ["Pilot:Password"] = Password,
            ["Pilot:ClientSecret"] = ClientSecret,
            ["Pilot:RedirectUri"] = RelyingParty.Callback
        }).Build();
        services = new ServiceCollection().AddSingleton<IConfiguration>(configuration).AddSingleton(TimeProvider.System)
            .AddScoped<PilotBootstrap>().AddScoped<PilotEligibility>()
            .Configure<PasswordHasherOptions>(options => options.IterationCount = 210_000)
            .AddLogging(logs => logs.ClearProviders())
            .AddIdentityPersistence(DatabaseProviderParser.Parse(provider), connection!)
            .BuildServiceProvider(validateScopes: true);
        RelyingParty.ExpectedIdentity = async cancellationToken =>
        {
            await using var scope = services.CreateAsyncScope();
            var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var manifest = await database.Set<PilotManifest>().SingleAsync(cancellationToken);
            var session = await database.Set<LocalIdentitySession>().Where(value => value.UserId == manifest.UserId && !value.Revoked)
                .OrderByDescending(value => value.AuthenticatedAt).FirstAsync(cancellationToken);
            return (manifest.UserId, session.AuthenticatedAt);
        };
    }

    public static HttpClient CreateBrowserClient() => new(new HttpClientHandler { CookieContainer = new CookieContainer(), AllowAutoRedirect = true })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    public async Task<TestFlow> AuthorizeAsync(HttpClient browser, bool exchange = false, string? prompt = null, string? maximumAge = null, TestFlow? prepared = null)
    {
        var flow = prepared ?? RelyingParty.Prepare(exchange, prompt, maximumAge);
        using var login = await browser.GetAsync(RelyingParty.StartUri(flow), TestContext.Current.CancellationToken);
        var html = await login.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
        if (login.RequestMessage!.RequestUri!.AbsolutePath == "/account/login")
        {
            var interaction = InputValue(html, "Interaction");
            var token = InputValue(html, "__RequestVerificationToken");
            using var form = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["Interaction"] = interaction,
                ["__RequestVerificationToken"] = token,
                ["Username"] = Username,
                ["Password"] = Password
            });
            using var completed = await browser.PostAsync(new Uri(Issuer, "/account/login"), form, TestContext.Current.CancellationToken);
            var completion = await completed.Content.ReadAsStringAsync(TestContext.Current.CancellationToken);
            // HTTP tests follow the real no-script continuation; browser tests exercise the actual script.
            if (completed.RequestMessage!.RequestUri!.AbsolutePath == "/account/complete")
            {
                var match = ContinueLink().Match(completion);
                Assert.True(match.Success, "The bound login did not produce a safe continuation.");
                using var callback = await browser.GetAsync(new Uri(Issuer, WebUtility.HtmlDecode(match.Groups[1].Value)), TestContext.Current.CancellationToken);
                Assert.True(callback.IsSuccessStatusCode, "The relying-party callback failed.");
            }
        }
        using var deadline = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        deadline.CancelAfter(TimeSpan.FromSeconds(15));
        Assert.True(await flow.Completion.Task.WaitAsync(deadline.Token), "The genuine login/callback/exchange flow failed. " + flow.ValidationFailure);
        return flow;
    }

    public static string InputValue(string html, string name)
    {
        var expression = new Regex("<input[^>]*name=\"" + Regex.Escape(name) + "\"[^>]*value=\"([^\"]+)\"", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(1));
        var match = expression.Match(html);
        Assert.True(match.Success, "The protected login form is missing a required field.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    [GeneratedRegex("id=\"continue-link\" href=\"([^\"]+)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ContinueLink();

    public async ValueTask DisposeAsync()
    {
        var failed = false;
        async Task Attempt(Func<Task> action)
        {
            try
            {
                await action();
            }
            catch (Exception) { failed = true; }
        }
        if (application is not null)
        {
            using var teardown = new CancellationTokenSource(TimeSpan.FromSeconds(45));
            await Attempt(() => application.StopAsync(teardown.Token));
            await Attempt(() => application.DisposeAsync().AsTask());
        }
        if (services is not null)
        {
            await Attempt(() => services.DisposeAsync().AsTask());
        }
        if (relyingParty is not null)
        {
            await Attempt(() => relyingParty.DisposeAsync().AsTask());
        }
        if (builder is not null)
        {
            await Attempt(() => builder.DisposeAsync().AsTask());
        }
        await Attempt(() => { credentials.Dispose(); return Task.CompletedTask; });
        if (lifecycle is not null)
        {
            await Attempt(lifecycle.AssertRemovedAsync);
        }
        Assert.False(failed, "Issuer cleanup failed; every remaining resource was still attempted.");
    }
}
