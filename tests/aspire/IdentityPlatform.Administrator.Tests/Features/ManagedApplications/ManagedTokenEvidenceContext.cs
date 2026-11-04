using IdentityPlatform.Administrator.Tests.Features.BrowserSessions;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.ManagedApplications;

/// <summary>Borrows an existing HTTPS fixture, exact managed registration and fresh read-scope factory without creating or owning authority.</summary>
internal sealed class ManagedTokenEvidenceContext(BrowserHttpsFixture environment, HttpClient transport, string applicationId,
    string clientId, Func<AsyncServiceScope> createScope)
{
    internal BrowserHttpsFixture Environment => environment;
    internal HttpClient Transport => transport;
    internal string Id => applicationId;
    internal string ClientIdentifier => clientId;

    /// <summary>Adapts an existing native fixture without creating another registration, ceremony or owning disposable wrapper.</summary>
    internal static ManagedTokenEvidenceContext Borrow(ManagedCacheTestContext test) =>
        new(test.Http.Environment, test.Http.IssuerClient, test.Id, test.ClientIdentifier, test.Http.Scope);

    /// <summary>Creates only an independently disposed read scope supplied by the existing fixture owner.</summary>
    internal AsyncServiceScope Scope() => createScope();

    /// <summary>Reads fresh committed state using the same shared snapshot implementation as original native callers.</summary>
    internal Task<ManagedCacheSnapshot> SnapshotAsync(bool applicationOnly = false) =>
        ManagedCacheSnapshotReader.ReadAsync(createScope, applicationId, applicationOnly);
}
