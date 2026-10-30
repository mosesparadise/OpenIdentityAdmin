using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.AspNetCore.Http;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Characterizes installed native caching and transaction effects on real providers; synthetic human seams are not managed product acceptance.</summary>
[Collection("administrator-bootstrap")]
public sealed class NativeCacheTests(BootstrapFixture fixture)
{
    private const long CachedAndCodeTokens = 2;
    private const string ForeignClient = "foreign-cache-client";
    private const string ForeignCallback = "https://foreign.example.test/callback";
    private const string ForeignState = "foreign-state";
    private const string ForeignScope = "foreign.scope";
    private const string ForeignChallenge = "foreign-challenge";

    /// <summary>Initial anonymous native sign-in creates only a request row; resume restores its protected values without consuming it or granting a subject.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_InitialAuthorization_When_NativeCacheResumes_Then_ExactDemandSurvivesOuterSubstitutionAndIssuesOnce(string provider)
    {
        await using var issuer = await NativeCacheFixture.CreateAsync(fixture, provider);
        var verifier = NativeCacheProfile.Random();
        var original = NativeCacheFlow.Initial(verifier);
        var resume = NativeCacheFlow.Resume(await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize, original));
        Assert.Equal(1, issuer.Fault.AnonymousSignIns);
        Assert.Equal((1L, 0L), await NativeCacheFlow.CountsAsync(issuer));
        var id = await NativeCacheFlow.RequestIdAsync(issuer, resume);
        await RequireRestoredAsync(issuer, original, resume, id);
        var foreign = new Dictionary<string, string>(resume, StringComparer.Ordinal) { [OpenIddictConstants.Parameters.ClientId] = ForeignClient };
        NativeCacheFlow.RequireRejection(await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize, foreign),
            OpenIddictConstants.Errors.InvalidRequest, StatusCodes.Status400BadRequest);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeCacheFlow.StatusAsync(issuer, id));
        var code = NativeCacheFlow.Code(await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize, resume, consent: true));
        Assert.Equal(OpenIddictConstants.Statuses.Redeemed, await NativeCacheFlow.StatusAsync(issuer, id));
        Assert.Equal((CachedAndCodeTokens, 1L), await NativeCacheFlow.CountsAsync(issuer));
        var codeId = await NativeCacheFlow.CodeIdAsync(issuer);
        NativeCacheFlow.RequireExchange(await NativeCacheFlow.ExchangeAsync(issuer, code, verifier));
        Assert.Equal(OpenIddictConstants.Statuses.Redeemed, await NativeCacheFlow.StatusAsync(issuer, codeId));
        NativeCacheFlow.RequireRejection(await NativeCacheFlow.ExchangeAsync(issuer, code, verifier),
            OpenIddictConstants.Errors.InvalidGrant, StatusCodes.Status400BadRequest);
    }

    /// <summary>A second valid registration reaches native cached-client binding; its own cache works but cannot resume another client's protected request.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_TwoValidRegisteredClients_When_CachedRequestIsSubstituted_Then_NativeRestorationRejectsForeignBinding(string provider)
    {
        await using var issuer = await NativeCacheFixture.CreateAsync(fixture, provider);
        var first = NativeCacheFlow.Resume(await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize,
            NativeCacheFlow.Initial(NativeCacheProfile.Random())));
        var second = NativeCacheFlow.Resume(await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize,
            NativeCacheFlow.Initial(NativeCacheProfile.Random(), NativeCacheProfile.SecondClient, NativeCacheProfile.SecondCallback)));
        var positive = await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize, second);
        Assert.False(positive.Handled);
        Assert.NotNull(positive.Cached);
        Assert.True(positive.Request!.ClientId == NativeCacheProfile.SecondClient);
        var substituted = new Dictionary<string, string>(first, StringComparer.Ordinal)
        {
            [OpenIddictConstants.Parameters.ClientId] = NativeCacheProfile.SecondClient
        };
        NativeCacheFlow.RequireRejection(await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize, substituted),
            OpenIddictConstants.Errors.InvalidRequest, StatusCodes.Status400BadRequest);
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeCacheFlow.StatusAsync(issuer, await NativeCacheFlow.RequestIdAsync(issuer, first)));
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeCacheFlow.StatusAsync(issuer, await NativeCacheFlow.RequestIdAsync(issuer, second)));
    }

    /// <summary>Only native restoration supplies the resumed demand; an outer query cannot change callback, scope, state or verifier challenge.</summary>
    private static async Task RequireRestoredAsync(NativeCacheFixture issuer, Dictionary<string, string> original,
        Dictionary<string, string> resume, string id)
    {
        var altered = new Dictionary<string, string>(resume, StringComparer.Ordinal)
        {
            [OpenIddictConstants.Parameters.RedirectUri] = ForeignCallback,
            [OpenIddictConstants.Parameters.State] = ForeignState,
            [OpenIddictConstants.Parameters.Scope] = ForeignScope,
            [OpenIddictConstants.Parameters.CodeChallenge] = ForeignChallenge
        };
        var response = await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize, altered);
        Assert.False(response.Handled);
        Assert.NotNull(response.Cached);
        Assert.Equal(OpenIddictConstants.RequestTokenTypes.Private.CachedAuthorizationRequest,
            response.Cached.GetClaim(OpenIddictConstants.Claims.Private.RequestTokenType));
        Assert.Null(response.Cached.GetClaim(OpenIddictConstants.Claims.Subject));
        Assert.True(response.Cached.GetTokenId() == id);
        Assert.NotNull(response.Request);
        foreach (var field in original)
        {
            Assert.True((string?)response.Request.GetParameter(field.Key) == field.Value);
        }
        Assert.Equal(OpenIddictConstants.Statuses.Valid, await NativeCacheFlow.StatusAsync(issuer, id));
        Assert.Equal((1L, 0L), await NativeCacheFlow.CountsAsync(issuer));
    }

    /// <summary>Proves early handler order alone cannot survive an enclosing rollback, while a committed late rejection retains actual redemption.</summary>
    [Theory]
    [InlineData("PostgreSql", false, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", false, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, false, Traits = ["Provider", "PostgreSql"])]
    [InlineData("PostgreSql", true, true, Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", false, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", false, true, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, false, Traits = ["Provider", "SqlServer"])]
    [InlineData("SqlServer", true, true, Traits = ["Provider", "SqlServer"])]
    public async Task Given_LateFailureAfterNativeRedemption_When_OwnerCommitsOrRollsBack_Then_PersistedReplayStateFollowsTheActualTransaction(
        string provider, bool codeArtifact, bool rollback)
    {
        await using var issuer = await NativeCacheFixture.CreateAsync(fixture, provider);
        var verifier = NativeCacheProfile.Random();
        var resume = NativeCacheFlow.Resume(await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize, NativeCacheFlow.Initial(verifier)));
        var code = codeArtifact ? NativeCacheFlow.Code(await NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize, resume, consent: true)) : null;
        var id = codeArtifact ? await NativeCacheFlow.CodeIdAsync(issuer) : await NativeCacheFlow.RequestIdAsync(issuer, resume);
        var before = await NativeCacheFlow.CountsAsync(issuer);
        issuer.Fault.Selected = rollback ? NativeCacheFailure.Throw : NativeCacheFailure.Reject;
        if (rollback)
        {
            var failure = await Record.ExceptionAsync(() => IssueAsync(issuer, resume, code, verifier));
            Assert.True(issuer.Fault.Caused(failure));
        }
        else
        {
            NativeCacheFlow.RequireRejection(await IssueAsync(issuer, resume, code, verifier), OpenIddictConstants.Errors.ServerError,
                codeArtifact ? StatusCodes.Status500InternalServerError : StatusCodes.Status302Found);
        }
        Assert.Equal(1, issuer.Fault.RedemptionHits);
        Assert.Equal(before, await NativeCacheFlow.CountsAsync(issuer));
        Assert.Equal(rollback ? OpenIddictConstants.Statuses.Valid : OpenIddictConstants.Statuses.Redeemed,
            await NativeCacheFlow.StatusAsync(issuer, id));
        issuer.Fault.Selected = NativeCacheFailure.None;
        var replay = await IssueAsync(issuer, resume, code, verifier);
        RequireReplay(replay, rollback, codeArtifact);
    }

    /// <summary>Reuses exactly the original client-held artifact after fault removal, without manufacturing another native principal.</summary>
    private static Task<NativeCacheResponse> IssueAsync(NativeCacheFixture issuer, Dictionary<string, string> resume, string? code, string verifier) =>
        code is null ? NativeCacheHttp.InvokeAsync(issuer, NativeCacheProfile.Authorize, resume, consent: true) : NativeCacheFlow.ExchangeAsync(issuer, code, verifier);

    /// <summary>States the observed native behavior explicitly; production integration must prevent the rollback resurrection demonstrated here.</summary>
    private static void RequireReplay(NativeCacheResponse response, bool rollback, bool codeArtifact)
    {
        if (!rollback)
        {
            NativeCacheFlow.RequireRejection(response, codeArtifact ? OpenIddictConstants.Errors.InvalidGrant : OpenIddictConstants.Errors.InvalidToken,
                StatusCodes.Status400BadRequest);
        }
        else if (codeArtifact)
        {
            NativeCacheFlow.RequireExchange(response);
        }
        else
        {
            Assert.False(string.IsNullOrEmpty(NativeCacheFlow.Code(response)));
        }
    }
}
