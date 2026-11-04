using System.Net;
using IdentityPlatform.Administrator.Tests.Fixtures;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Rechecks actual current policy and human authority at native authorization and exchange, after the earlier form proof was accepted.</summary>
[Collection("administrator-bootstrap")]
public sealed class ManagedTokenDriftTests(BootstrapFixture fixture)
{
    private const string CredentialBypass = "managed-current-credential-bypass";
    private const string CriticalDecision = "CriticalDecision";
    private const string CredentialDecision = "managed-current-credential";

    private const string InvalidGrant = "invalid_grant";
    private const string InvalidToken = "invalid_token";
    private const string PostgreSql = "PostgreSql";
    private const string SqlServer = "SqlServer";
    private const string ProviderTrait = "Provider";
    private const int ObservationAdvance = 1;
    private const string Valid = "valid";
    private const string Redeemed = "redeemed";
    private static readonly string[] Providers = [PostgreSql, SqlServer];
    private static readonly bool[] Phases = [false, true];

    /// <summary>Independent authority changes after approval/code issuance cannot create another artifact, audit or administrator grant.</summary>
    [Theory, MemberData(nameof(Cases))]
    public async Task Given_ApprovedManagedProof_When_CurrentPolicyOrAccountAuthorityChanges_Then_NativeIssuanceCannotPublish(string provider, bool exchange, ManagedHumanAuthority defect)
    {
        await using var test = await ManagedCacheTestContext.CreateAsync(fixture, provider);
        var flow = new ManagedTokenFlow(test);
        await flow.ApproveAsync();
        var code = exchange ? await flow.CodeAsync() : null;
        test.Http.Environment.Issuer.Account.Journey.Clock.Now = test.Http.Now.AddSeconds(ObservationAdvance);
        var authority = await ManagedHumanEvidence.AuthorityAsync(test);
        await ManagedHumanDrift.WithAsync(test, defect, async () =>
        {
            var before = await ManagedTokenEvidence.RollbackDigestAsync(test,
                observed: !exchange && defect is not (ManagedHumanAuthority.Policy or ManagedHumanAuthority.Credential) ? test.Http.Now.ToUnixTimeSeconds() : null);
            using var response = exchange ? await flow.ExchangeAsync(code!) : await test.SendAsync(flow.Resume);
            if (exchange && defect == ManagedHumanAuthority.Credential)
            {
                var early = await test.SnapshotAsync();
                var codeId = Assert.Single(early.Interactions).CodeId;
                Assert.False(early.Tokens.Single(value => value.Id == codeId).Status == Redeemed, CredentialBypass);
            }
            if (defect is ManagedHumanAuthority.Policy or ManagedHumanAuthority.Credential)
            {
                await ManagedTokenEvidence.ErrorAsync(response, HttpStatusCode.BadRequest, exchange ? InvalidGrant : InvalidToken, local: !exchange);
            }
            else
            {
                await ManagedCacheTestContext.UnavailableAsync(response);
            }
            Assert.Equal(before, await ManagedTokenEvidence.RollbackDigestAsync(test));
            var snapshot = await test.SnapshotAsync();
            var interaction = Assert.Single(snapshot.Interactions);
            var consumed = snapshot.Tokens.Single(row => row.Id == (exchange ? interaction.CodeId : interaction.RequestId));
            Assert.True(consumed.Status == (defect is ManagedHumanAuthority.Policy or ManagedHumanAuthority.Credential ? Valid : Redeemed));
            Assert.Equal(authority, await ManagedHumanEvidence.AuthorityAsync(test));
        });
    }

    /// <summary>Preserves explicit provider, native phase and independent authority-change identities in discovery.</summary>
    public static IEnumerable<TheoryDataRow<string, bool, ManagedHumanAuthority>> Cases =>
        from provider in Providers
        from exchange in Phases
        from defect in Enum.GetValues<ManagedHumanAuthority>()
        select Case(provider, exchange, defect);

    /// <summary>Tags only exact credential-expiry equality after real code issuance for the mutation witness.</summary>
    private static TheoryDataRow<string, bool, ManagedHumanAuthority> Case(string provider, bool exchange, ManagedHumanAuthority defect)
    {
        var row = new TheoryDataRow<string, bool, ManagedHumanAuthority>(provider, exchange, defect).WithTrait(ProviderTrait, provider);
        return exchange && defect == ManagedHumanAuthority.Credential ? row.WithTrait(CriticalDecision, CredentialDecision) : row;
    }
}
