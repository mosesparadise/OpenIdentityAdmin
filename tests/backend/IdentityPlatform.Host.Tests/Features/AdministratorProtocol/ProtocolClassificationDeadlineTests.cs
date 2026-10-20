using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Primitives;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Proves asynchronous raw classification cannot renew the original transport deadline or release a still-running owned scope.</summary>
public sealed class ProtocolClassificationDeadlineTests(AdministratorRegistrationFixture fixture) : IClassFixture<AdministratorRegistrationFixture>
{
    private const string Callback = "https://administrator.example.test/callback";
    private const string TokenPath = "/connect/token";
    private const string IssuerHost = "issuer.example.test";
    private const string Https = "https";
    private const string FormType = "application/x-www-form-urlencoded";
    private const int ClassificationSeconds = 8;
    private const int RemainingSeconds = 2;
    private static readonly byte[] Staged = [1, 2, 3];

    /// <summary>Advances the original deadline while downstream deliberately ignores cancellation; no response bytes or scope disposal occur early.</summary>
    [Fact]
    public async Task Given_DelayedFormClassification_When_OwnedWorkIgnoresCancellation_Then_OriginalDeadlineAbortsWithoutDetachingWork()
    {
        var clock = new ControlledProbeClock();
        await using var storage = new StorageRequestProbe(Configure, clock);
        await using var scope = storage.Root.CreateAsyncScope();
        using var output = new MemoryStream();
        var http = Create(scope.ServiceProvider, output);
        var form = new DelayedForm();
        var lifetime = new ObservedLifetime();
        http.Features.Set<IFormFeature>(form);
        http.Features.Set<IHttpRequestLifetimeFeature>(lifetime);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var run = ProtocolHosting.InvokeAsync(http, async context =>
        {
            await context.Response.Body.WriteAsync(Staged, TestContext.Current.CancellationToken);
            entered.SetResult();
            await release.Task;
        });
        try
        {
            await form.Entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            clock.Advance(TimeSpan.FromSeconds(ClassificationSeconds));
            form.Release.SetResult();
            await entered.Task.WaitAsync(TestContext.Current.CancellationToken);
            Assert.Equal(0, lifetime.Aborts);
            clock.Advance(TimeSpan.FromSeconds(RemainingSeconds));
            Assert.True(lifetime.Aborts > 0);
            Assert.False(run.IsCompleted);
            Assert.Empty(output.ToArray());
            Assert.Equal(0, Assert.Single(storage.Databases).Disposals);
        }
        finally { form.Release.TrySetResult(); release.TrySetResult(); await run; }
        Assert.Empty(output.ToArray());
        Assert.Equal(1, Assert.Single(storage.Databases).Disposals);
        Assert.Same(scope.ServiceProvider, http.RequestServices);
    }

    /// <summary>Supplies the exact shared-host configuration with unopened native contexts; this test exercises no SQL.</summary>
    private void Configure(IServiceCollection services)
    {
        services.AddSingleton(new ProtocolProfiles(true, AdministratorRegistrationPolicy.Create(Callback, fixture.Key)));
        services.AddScoped<ProtocolHostRequest>();
        services.AddOptions<OpenIddictServerOptions>().Configure(options => options.TokenEndpointUris.Add(new Uri(TokenPath, UriKind.Relative)));
    }

    /// <summary>Creates an ordinary form request whose host-owned response stream remains observable outside staging.</summary>
    private static DefaultHttpContext Create(IServiceProvider services, Stream output)
    {
        var http = new DefaultHttpContext { RequestServices = services, RequestAborted = TestContext.Current.CancellationToken };
        http.Request.Scheme = Https;
        http.Request.Host = new HostString(IssuerHost);
        http.Request.Path = TokenPath;
        http.Request.Method = HttpMethods.Post;
        http.Request.ContentType = FormType;
        http.Response.Body = output;
        return http;
    }

    /// <summary>Retains one asynchronous raw read until explicitly released, without fabricating an authenticated request.</summary>
    private sealed class DelayedForm : IFormFeature
    {
        private const string ClientId = "client_id";
        private const string AdministratorClient = "identity-admin-bff";
        internal TaskCompletionSource Entered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        internal TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public bool HasFormContentType => true;
        public IFormCollection? Form
        {
            get; set;
        }

        /// <summary>Rejects unexpected synchronous parsing in this asynchronous transport regression.</summary>
        public IFormCollection ReadForm() => throw new InvalidOperationException();

        /// <summary>Waits on the controlled classification read while preserving the supplied cancellation token.</summary>
        public async Task<IFormCollection> ReadFormAsync(CancellationToken cancellationToken)
        {
            Entered.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
            return Form = new FormCollection(new Dictionary<string, StringValues> { [ClientId] = AdministratorClient });
        }
    }

    /// <summary>Records actual HttpContext.Abort independently of cooperative cancellation.</summary>
    private sealed class ObservedLifetime : IHttpRequestLifetimeFeature
    {
        public CancellationToken RequestAborted { get; set; } = TestContext.Current.CancellationToken;
        internal int Aborts
        {
            get; private set;
        }

        /// <summary>Records transport termination without completing the deliberately blocked operation.</summary>
        public void Abort() => Aborts++;
    }
}
