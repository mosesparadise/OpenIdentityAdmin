using System.Net;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Reconciles race publication with exact committed exchange audits and native token ownership, allowing replay to preempt both responses.</summary>
internal static class ManagedTokenRaceEvidence
{
    private const string InvalidGrant = "invalid_grant";
    private const string Access = "urn:ietf:params:oauth:token-type:access_token";
    private const string Identity = "urn:ietf:params:oauth:token-type:id_token";
    private const string Valid = "valid";
    private const string Revoked = "revoked";
    private const int ExchangeTokens = 2;

    /// <summary>Requires zero or one complete audited issuance; arbitrary failures, extra token types or unrelated owner bindings cannot satisfy the race.</summary>
    internal static async Task<ManagedCacheSnapshot> RequireAsync(ManagedCacheTestContext test, ManagedCacheSnapshot before, HttpResponseMessage[] responses)
    {
        foreach (var response in responses.Where(response => response.StatusCode != HttpStatusCode.OK))
        {
            if (response.StatusCode == HttpStatusCode.BadRequest)
            {
                await ManagedTokenEvidence.ErrorAsync(response, HttpStatusCode.BadRequest, InvalidGrant);
            }
            else
            {
                await ManagedCacheTestContext.UnavailableAsync(response);
            }
        }
        var audits = await ManagedTokenEvidence.ExchangeAuditsAsync(test);
        var after = await test.SnapshotAsync();
        var row = Assert.Single(before.Interactions);
        Assert.True(audits is 0 or 1 && responses.Count(response => response.StatusCode == HttpStatusCode.OK) <= audits);
        Assert.True(before.Tokens.All(token => after.Tokens.Any(value => value.Id == token.Id)));
        var added = after.Tokens.Where(token => before.Tokens.All(value => value.Id != token.Id)).ToArray();
        Assert.Equal(ExchangeTokens * audits, added.Length);
        if (audits != 0)
        {
            Assert.True(added.Select(token => token.Type).ToHashSet(StringComparer.Ordinal).SetEquals([Access, Identity]));
            Assert.All(added, token => Assert.True(token.Authorization?.Id == row.AuthorizationId && token.Subject == row.UserId));
        }
        Assert.Equal(before.Authorizations, after.Authorizations);
        Assert.Equal(FactorBinding.Digest(before.Interactions), FactorBinding.Digest(after.Interactions));
        Assert.True(after.Tokens.Single(token => token.Id == row.CodeId).Status != Valid);
        return after;
    }

    /// <summary>Final actual replay cannot add output/audit and must leave every issued access/identity credential in the affected chain revoked.</summary>
    internal static async Task RequireFinalReplayAsync(ManagedCacheTestContext test, ManagedCacheSnapshot before)
    {
        var after = await test.SnapshotAsync();
        Assert.Equal(before.Tokens.Select(row => row.Id), after.Tokens.Select(row => row.Id));
        var outputs = after.Tokens.Where(row => row.Type is Access or Identity).ToArray();
        Assert.Equal(ExchangeTokens * await ManagedTokenEvidence.ExchangeAuditsAsync(test), outputs.Length);
        Assert.All(outputs, row => Assert.True(row.Status == Revoked));
    }
}
