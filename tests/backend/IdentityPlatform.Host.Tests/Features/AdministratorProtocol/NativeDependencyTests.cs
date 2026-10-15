using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Client;
using OpenIddict.EntityFrameworkCore;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.AdministratorProtocol;

/// <summary>Checks the pinned native extension boundaries before administrator composition is implemented.</summary>
public sealed class NativeDependencyTests
{
    /// <summary>Checks the descriptor window after the native redemption handler and before native creation and response dispatch.</summary>
    [Fact]
    public void Given_PinnedNativeHandlers_When_LocatingTransactionWindows_Then_RedemptionAndResponseRemainOutside()
    {
        const int HookOffset = 500;
        const int NativeResponseOrder = 500000;
        var begin = OpenIddictServerHandlers.RestoreInternalClaims.Descriptor.Order + HookOffset;
        const int Commit = 499000;
        Assert.True(OpenIddictServerHandlers.RedeemTokenEntry.Descriptor.Order < begin);
        Assert.True(begin < OpenIddictServerHandlers.AttachAuthorization.Descriptor.Order);
        Assert.True(OpenIddictServerHandlers.AttachSignInParameters.Descriptor.Order < Commit);
        Assert.Equal(NativeResponseOrder, OpenIddictServerHandlers.Exchange.ApplyTokenResponse<OpenIddictServerEvents.ProcessSignInContext>.Descriptor.Order);
        Assert.Equal(NativeResponseOrder, OpenIddictServerHandlers.Authentication.ApplyAuthorizationResponse<OpenIddictServerEvents.ProcessSignInContext>.Descriptor.Order);
        Assert.Equal(NativeResponseOrder, OpenIddictServerHandlers.Authentication.ApplyPushedAuthorizationResponse<OpenIddictServerEvents.ProcessSignInContext>.Descriptor.Order);
        var proof = OpenIddictServerHandlers.ValidateClientCertificate.Descriptor.Order + HookOffset;
        Assert.True(OpenIddictServerHandlers.ValidateClientAssertionIssuer.Descriptor.Order < proof);
        Assert.True(proof < OpenIddictServerHandlers.ValidateRequestToken.Descriptor.Order);
        Assert.True(OpenIddictClientHandlers.Protection.CreateTokenEntry.Descriptor.Order <
            OpenIddictClientHandlers.Protection.AttachTokenPayload.Descriptor.Order);
        Assert.True(OpenIddictClientHandlers.GenerateLoginStateToken.Descriptor.Order <
            OpenIddictClientHandlers.SendPushedAuthorizationRequest.Descriptor.Order);
        Assert.True(OpenIddictClientHandlers.RedeemStateTokenEntry.Descriptor.Order <
            OpenIddictClientHandlers.HandleFrontchannelErrorResponse.Descriptor.Order);
    }

    /// <summary>Checks native context reference identity in separate component providers without opening a database.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_SeparateComponentProviders_When_ResolvingNativeCore_Then_UsesTheExactScopedContext(bool client)
    {
        const string Connection = "Host=localhost;Database=synthetic_dependency_probe";
        var services = new ServiceCollection().AddLogging();
        if (client)
        {
            services.AddSessionPersistence(DatabaseProvider.PostgreSql, Connection);
            services.AddOpenIddict().AddCore(core => core.UseEntityFrameworkCore().UseDbContext<AdminSessionDbContext>());
        }
        else
        {
            services.AddIdentityPersistence(DatabaseProvider.PostgreSql, Connection);
        }
        await using var provider = services.BuildServiceProvider();
        await using var scope = provider.CreateAsyncScope();
        var native = scope.ServiceProvider.GetRequiredService<IOpenIddictEntityFrameworkCoreContext>();
        var actual = await native.GetDbContextAsync(TestContext.Current.CancellationToken);
        object expected = client ? scope.ServiceProvider.GetRequiredService<AdminSessionDbContext>() :
            scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Same(expected, actual);
    }
}
