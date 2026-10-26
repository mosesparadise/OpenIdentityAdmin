using IdentityPlatform.Administrator.Tests.Features.Factors;
using IdentityPlatform.Administrator.Tests.Features.ProtocolAdmission;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Server;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.BrowserSessions;

/// <summary>Exercises audit-header denial and rollback through authenticated native PAR on both real providers.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeBrowserAuditTests(BootstrapFixture fixture)
{
    private const string Format = "N";
    private const string Missing = "missing";
    private const string Duplicate = "duplicate";
    private const string Malformed = "malformed";
    private const string InvalidAssertion = "invalid-assertion";
    private const string Header = "Identity-Browser-Audit-Correlation";
    private const string Other = "0123456789abcdef0123456789abcdef";
    private const string ProviderTrait = "Provider";
    private static readonly string[] Faults = [Missing, Duplicate, Malformed];

    /// <summary>Discovers each malformed authenticated wire shape on both relational implementations.</summary>
    public static IEnumerable<TheoryDataRow<string, string>> Cases => from provider in IssuerContinuationProviderFixture.Providers
                                                                      from fault in Faults
                                                                      select new TheoryDataRow<string, string>(provider, fault).WithTrait(ProviderTrait, provider);

    /// <summary>Malformed provenance spends the authentic assertion but creates no native token, attachment or link; a fresh valid demand succeeds.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_CompletedLogin_When_AuditHeaderIsInvalid_Then_NoAttachmentOrLinkCommits(string provider, string fault)
    {
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider);
        var before = await flow.AttachmentSnapshotAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        var assertions = await flow.AssertionsAsync();
        var result = await flow.PushAsync(configureHttp: http =>
        {
            http.Request.Headers.Remove(Header);
            if (fault == Duplicate)
            {
                http.Request.Headers[Header] = new Microsoft.Extensions.Primitives.StringValues([Other, Other]);
            }
            if (fault == Malformed)
            {
                http.Request.Headers[Header] = Guid.Empty.ToString(Format);
            }
        });
        NativeBrowserParFixture.RequireUnavailable(result);
        Assert.Equal(assertions + 1, await flow.AssertionsAsync());
        Assert.Equal(counts, await NativeAccountPersistence.CountAsync(flow.Scenario));
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        var original = await flow.RowAsync();
        _ = await flow.RequireAttachedAsync(await flow.PushAsync(), original, flow.Journey.Clock.Now.ToUnixTimeSeconds());
    }

    /// <summary>A changed value after real native token persistence cannot escape the frozen issuer attachment guard.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_FrozenAuditCorrelation_When_HeaderChangesAfterNativePersistence_Then_AttachmentRollsBack(string provider)
    {
        var observed = new NativeBrowserParObservation { Armed = true };
        var changed = 0;
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider, configure: services =>
        {
            observed.Configure(services);
            services.AddOpenIddict().AddServer().AddEventHandler<OpenIddictServerEvents.GenerateTokenContext>(handler => handler
                .SetOrder(OpenIddictServerHandlers.Protection.AttachTokenPayload.Descriptor.Order + AfterObservation)
                .UseInlineHandler(context =>
                {
                    if (context.EndpointType == OpenIddictServerEndpointType.PushedAuthorization && observed.Armed)
                    {
                        Assert.Equal(1, observed.Reached);
                        context.Transaction.GetHttpRequest()!.Headers[Header] = Other;
                        changed++;
                    }
                    return default;
                }));
        });
        var before = await flow.AttachmentSnapshotAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        NativeBrowserParFixture.RequireUnavailable(await flow.PushAsync());
        Assert.Equal(1, changed);
        Assert.Null(await NativeAccountPersistence.StatusAsync(flow.Scenario, observed.Identifier));
        Assert.Equal(counts, await NativeAccountPersistence.CountAsync(flow.Scenario));
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        observed.Armed = false;
        var original = await flow.RowAsync();
        _ = await flow.RequireAttachedAsync(await flow.PushAsync(), original, flow.Journey.Clock.Now.ToUnixTimeSeconds());
    }
    private const int AfterObservation = 2;

    /// <summary>Neither metadata alone nor an unauthenticated completed demand can create an audit link; ordinary authenticated B2 remains usable.</summary>
    [Theory, MemberData(nameof(StoreTests.Providers), MemberType = typeof(StoreTests))]
    public async Task Given_CanonicalAuditMetadata_When_CeremonyOrClientAuthenticationIsAbsent_Then_NoLinkOrNativeTokenCommits(string provider)
    {
        await using var flow = await NativeBrowserParFixture.CreateAsync(fixture, provider);
        var before = await flow.AttachmentSnapshotAsync();
        var counts = await NativeAccountPersistence.CountAsync(flow.Scenario);
        var headerOnly = await NativeAccountHttp.InvokeAsync(flow.Scenario, AdministratorProtocolScenario.ParPath,
            NativeAccountFlow.PushFields(flow.Scenario), services: flow.Services,
            configureHttp: http => http.Request.Headers[Header] = Other);
        NativeBrowserParFixture.RequireUnavailable(headerOnly);
        NativeAccountDenialTests.RequireJson(await flow.PushAsync(assertion: InvalidAssertion), OpenIddictConstants.Errors.InvalidClient);
        Assert.Equal(counts, await NativeAccountPersistence.CountAsync(flow.Scenario));
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        _ = await NativeAccountFlow.PushAsync(flow.Scenario, flow.Services);
        Assert.True(before == await flow.AttachmentSnapshotAsync());
        var original = await flow.RowAsync();
        _ = await flow.RequireAttachedAsync(await flow.PushAsync(), original, flow.Journey.Clock.Now.ToUnixTimeSeconds());
    }
}
