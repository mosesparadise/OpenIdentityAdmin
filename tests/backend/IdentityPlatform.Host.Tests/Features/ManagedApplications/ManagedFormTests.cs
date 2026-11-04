using IdentityPlatform.AuthServer.Features.Accounts.ManagedApplications;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.ManagedApplications;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolTransport;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Checks native protection purpose, exact routes and immutable response publication without pretending to exercise database authentication.</summary>
public sealed class ManagedFormTests
{
    private const string Cookie = "Cookie";
    private const string Page = "/ManagedApplications/Index";
    private const string Route = "account/application/sign-in";
    private const string Alias = "account/application/sign-in/alias";
    private const string FormPage = "/Features/Accounts/ManagedApplications/Index.cshtml";
    private const string ForeignPurpose = "IdentityPlatform.ManagedApplications.Browser.v1";
    private const string Header = "X-Unplanned";
    private const string Corruption = "!";
    private const int LifetimeSeconds = 300;
    private const int OversizedLength = 4097;

    /// <summary>Only the conjunction of explicit administrator and managed capabilities retains the fixed endpoint and its owned marker.</summary>
    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public void Given_ManagedRazorPage_When_CapabilitiesAndAliasesAreEvaluated_Then_OnlyExplicitExactRouteIsRetained(bool administrator, bool managed)
    {
        var model = new PageRouteModel(FormPage, Page);
        model.Selectors.Add(new SelectorModel { AttributeRouteModel = new AttributeRouteModel { Template = Route } });
        model.Selectors.Add(new SelectorModel { AttributeRouteModel = new AttributeRouteModel { Template = Alias } });
        new AccountPageConvention(false, administrator, managed: managed).Apply(model);
        if (administrator && managed)
        {
            Assert.Single(Assert.Single(model.Selectors).EndpointMetadata.OfType<ManagedAccountEndpoint>());
        }
        else
        {
            Assert.Empty(model.Selectors);
        }
    }

    /// <summary>Actual native protection cannot cross browser/purpose, corruption, oversized bounds or the original expiry equality.</summary>
    [Theory]
    [InlineData(ManagedFormDefect.Valid)]
    [InlineData(ManagedFormDefect.Browser)]
    [InlineData(ManagedFormDefect.Purpose)]
    [InlineData(ManagedFormDefect.Corrupt)]
    [InlineData(ManagedFormDefect.Oversized)]
    [InlineData(ManagedFormDefect.Expired)]
    public void Given_ProtectedManagedForm_When_BindingOrLifetimeChanges_Then_OnlyOriginalLiveProofCanBeRead(ManagedFormDefect defect)
    {
        var clock = new FormClock();
        using var services = new ServiceCollection().AddSingleton<IDataProtectionProvider>(new EphemeralDataProtectionProvider())
            .AddSingleton<TimeProvider>(clock).BuildServiceProvider();
        var http = Context(services);
        var browser = ManagedBrowser.GetOrCreate(http);
        http.Request.Headers[Cookie] = http.Response.Headers.SetCookie.ToString().Split(';')[0];
        var request = Request(browser, clock.Now);
        var encoded = ManagedFormProof.Protect(http, request);
        switch (defect)
        {
            case ManagedFormDefect.Browser:
                http.Request.Headers.Remove(Cookie);
                ManagedBrowser.GetOrCreate(http);
                http.Request.Headers[Cookie] = http.Response.Headers.SetCookie.Last()!.Split(';')[0];
                break;
            case ManagedFormDefect.Purpose:
                encoded = services.GetRequiredService<IDataProtectionProvider>().CreateProtector(ForeignPurpose).Protect(encoded);
                break;
            case ManagedFormDefect.Corrupt:
                encoded = encoded[..^1] + Corruption;
                break;
            case ManagedFormDefect.Oversized:
                encoded = new string('A', OversizedLength);
                break;
            case ManagedFormDefect.Expired:
                clock.Now = clock.Now.AddSeconds(LifetimeSeconds);
                break;
        }
        var actual = ManagedFormProof.Read(http, encoded);
        Assert.Equal(defect == ManagedFormDefect.Valid, actual is not null);
        if (actual is not null)
        {
            Assert.True(actual.Selector == request.Selector && actual.Correlation == request.Correlation && actual.BrowserDigest == request.BrowserDigest);
            Assert.Equal(request.ExpiresAt, actual.ExpiresAt);
        }
    }

    /// <summary>A real deferred native response callback cannot mutate the frozen rendered form, redirect status or headers at seal time.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_RenderedManagedOutput_When_SealCallbackChangesHeaders_Then_FrozenPublicationRejectsMutation(bool mutate)
    {
        using var response = new ProtocolResponseCapture(new ProtocolStorageRequest(TimeProvider.System, CancellationToken.None));
        response.OnStarting(_ =>
        {
            if (mutate)
            {
                response.Headers[Header] = bool.TrueString;
            }
            return Task.CompletedTask;
        }, response);
        var frozen = new ManagedResponseFreeze(response);
        await response.SealAsync();
        if (mutate)
        {
            Assert.Throws<InvalidOperationException>(frozen.Require);
        }
        else
        {
            frozen.Require();
        }
    }

    /// <summary>Builds a genuine protected browser context with an explicit secure origin and no account authentication.</summary>
    private static DefaultHttpContext Context(IServiceProvider services) => new() { RequestServices = services };

    /// <summary>Provides only finite well-shaped form values; actual durable authentication remains tested against real providers.</summary>
    private static ManagedHumanRequest Request(string browser, DateTimeOffset now) => new()
    {
        Selector = ManagedLoginRules.CreateSecret(),
        BrowserDigest = ManagedLoginRules.Digest(browser),
        Correlation = CeremonySecrets.Create(),
        ExpiresAt = now.AddSeconds(LifetimeSeconds).ToUnixTimeSeconds(),
        Revision = 1,
        State = ManagedLoginState.PendingPassword
    };

    /// <summary>Controls wall-clock expiry without weakening native protection.</summary>
    private sealed class FormClock : TimeProvider
    {
        internal DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
        /// <summary>Returns the current explicitly selected form admission time.</summary>
        public override DateTimeOffset GetUtcNow() => Now;
    }
}

/// <summary>Names independent protected-form rejection causes.</summary>
public enum ManagedFormDefect
{
    Valid, Browser, Purpose, Corrupt, Oversized, Expired
}
