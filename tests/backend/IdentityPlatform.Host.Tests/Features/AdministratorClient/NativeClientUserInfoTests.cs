using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Proves the actual native evaluator cannot opt into a bearer-bearing UserInfo request for the fixed client.</summary>
public sealed class NativeClientUserInfoTests
{
    private const string Foreign = "https://unapproved.example/userinfo";
    private const string SyntheticToken = "synthetic-unusable-access-token";

    /// <summary>A control first demonstrates native default sending, then applies the real fixed-profile disable handler.</summary>
    [Fact]
    public async Task Given_OptionalUserInfoMetadata_When_NativeCodeFlowEvaluationRuns_Then_ProfilePolicyPreventsAnySend()
    {
        var context = Context();
        var native = new OpenIddictClientHandlers.EvaluateUserInfoRequest();
        await native.HandleAsync(context);
        Assert.True(context.SendUserInfoRequest);
        await new NativeClientUserInfo().HandleAsync(context);
        await native.HandleAsync(context);
        Assert.False(context.SendUserInfoRequest);
        await new NativeClientUserInfoFence().HandleAsync(context);
    }

    /// <summary>The final guard denies both a forced send and a late reversal of the no-retrieval flag.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_LateUserInfoOverride_When_FinalSendGuardRuns_Then_RequestIsDenied(bool send)
    {
        var context = Context();
        context.SendUserInfoRequest = send;
        context.DisableUserInfoRetrieval = send;
        await Assert.ThrowsAsync<InvalidOperationException>(() => new NativeClientUserInfoFence().HandleAsync(context).AsTask());
    }

    /// <summary>Supplies the minimal real code-flow context that would otherwise trigger native optional retrieval.</summary>
    private static OpenIddictClientEvents.ProcessAuthenticationContext Context() => new(new OpenIddictClientTransaction
    {
        Configuration = new() { UserInfoEndpoint = new(Foreign) }
    })
    {
        GrantType = OpenIddictConstants.GrantTypes.AuthorizationCode,
        BackchannelAccessToken = SyntheticToken
    };
}
