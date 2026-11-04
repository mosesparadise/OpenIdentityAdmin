using IdentityPlatform.AuthServer.Features.Protocol.ManagedApplications;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.ManagedApplications;

/// <summary>Proves managed-only demand restrictions independently of the shared native administrator/pilot option union.</summary>
public sealed class ManagedDemandTests
{
    private const string Client = "managed-client";
    private const string Callback = "https://managed.example.test/callback";
    private const string Correlation = "correlation";
    private const string AdministratorScope = "identity.admin";
    private const string MixedScope = "openid identity.admin";
    private const int CorrelationLimit = 512;
    private const int ChallengeLength = 43;

    /// <summary>Initial demand must remain identity-only and require bounded correlation even when native options support broader fixed profiles.</summary>
    [Theory]
    [InlineData(ManagedIntent.Valid, true)]
    [InlineData(ManagedIntent.Login, true)]
    [InlineData(ManagedIntent.Boundary, true)]
    [InlineData(ManagedIntent.AdministratorScope, false)]
    [InlineData(ManagedIntent.MixedScope, false)]
    [InlineData(ManagedIntent.Silent, false)]
    [InlineData(ManagedIntent.MissingState, false)]
    [InlineData(ManagedIntent.MissingNonce, false)]
    [InlineData(ManagedIntent.LongState, false)]
    [InlineData(ManagedIntent.LongNonce, false)]
    public void Given_InitialManagedIntent_When_ProfileDemandRead_Then_OnlyExactManagedPurposeAndBoundedCorrelationAreAccepted(ManagedIntent kind, bool accepted)
    {
        var request = new OpenIddictRequest
        {
            ClientId = Client,
            RedirectUri = Callback,
            ResponseType = OpenIddictConstants.ResponseTypes.Code,
            Scope = kind switch
            {
                ManagedIntent.AdministratorScope => AdministratorScope,
                ManagedIntent.MixedScope => MixedScope,
                _ => OpenIddictConstants.Scopes.OpenId
            },
            Prompt = kind == ManagedIntent.Silent ? OpenIddictConstants.PromptValues.None : kind == ManagedIntent.Login ? OpenIddictConstants.PromptValues.Login : null,
            CodeChallengeMethod = OpenIddictConstants.CodeChallengeMethods.Sha256,
            CodeChallenge = new string('A', ChallengeLength),
            State = kind == ManagedIntent.MissingState ? null : kind == ManagedIntent.LongState ? new string('s', CorrelationLimit + 1) : Correlation,
            Nonce = kind == ManagedIntent.MissingNonce ? null : kind == ManagedIntent.LongNonce ? new string('n', CorrelationLimit + 1) : Correlation
        };
        if (kind == ManagedIntent.Boundary)
        {
            request.State = new string('s', CorrelationLimit);
            request.Nonce = new string('n', CorrelationLimit);
        }
        var demand = ManagedDemand.Read(request);
        Assert.Equal(accepted, demand is not null);
        if (accepted)
        {
            Assert.False(demand!.Cached);
            Assert.True(demand.Matches(request));
        }
    }
}

/// <summary>Names independently selected initial managed profile and correlation boundary scenarios.</summary>
public enum ManagedIntent
{
    Valid, Login, Boundary, AdministratorScope, MixedScope, Silent, MissingState, MissingNonce, LongState, LongNonce
}
