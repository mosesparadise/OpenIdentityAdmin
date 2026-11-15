using System.Net;
using System.Text.Json;
using IdentityPlatform.Administrator.Tests.Fixtures;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.ApplicationGrants;
using IdentityPlatform.Infrastructure.Features.Applications;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Requires real HTTPS retirement admission and exact replay through independent bearer and cookie/CSRF boundaries.</summary>
[Collection("administrator-bootstrap")]
public sealed class ApplicationRetirementHttpTests(BootstrapFixture fixture)
{
    private const int TerminalRevision = 2;

    private const string ApplicationIdField = "applicationId";
    private const string ApplicationVersionField = "applicationVersion";
    private const string RequestKeyField = "requestKey";
    private const string JobField = "job";
    private const string IdField = "id";
    private const string VersionField = "version";
    private const string RetiredField = "retired";
    private const string EditableField = "editable";
    private const string EnabledField = "enabled";
    private const string Profile = "managed-web-v1";
    private const string ProfileField = "profile";

    private const string Body = "{\"reason\":\"security\",\"confirm\":true}";
    private const string Retirement = "/retirement";
    private const string Grants = "/grant-revocations/";
    private const string Separator = "/";
    private const string GuidFormat = "D";

    /// <summary>Commits one terminal application version and new durable job, then recovers the same admission from the other authenticated surface.</summary>
    [Theory]
    [InlineData("PostgreSql", Traits = ["Provider", "PostgreSql"])]
    [InlineData("SqlServer", Traits = ["Provider", "SqlServer"])]
    public async Task Given_GenuineAdministrator_When_RetirementIsReplayedAcrossHttpsSurfaces_Then_OneTerminalVersionAndJobRemainBound(string provider)
    {
        await using var test = await ApplicationHttpFixture.CreateAsync(fixture, provider);
        using var created = await test.CreateAsync(ApplicationHttpTests.Input(test.Now));
        var application = await ApplicationHttpTests.CreatedAsync(created);
        var id = application.Application.Id;
        var original = created.Headers.ETag!.Tag;
        var key = Guid.NewGuid().ToString(GuidFormat);
        string? jobId = null;
        foreach (var direct in new[] { true, false })
        {
            var path = (direct ? ApplicationHttpFixture.ApiCollection : ApplicationHttpFixture.Collection) + Separator + Uri.EscapeDataString(id);
            var proof = direct ? null : await test.TokenAsync();
            using var accepted = await test.CatalogueAsync(HttpMethod.Post, path + Retirement, Body, original, direct: direct, csrf: proof, key: key);
            Assert.Equal(HttpStatusCode.Accepted, accepted.StatusCode);
            using var json = JsonDocument.Parse(await accepted.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
            var receipt = json.RootElement;
            Assert.Equal(id, receipt.GetProperty(ApplicationIdField).GetString());
            Assert.Equal(ApplicationVersion.Create(id, TerminalRevision), receipt.GetProperty(ApplicationVersionField).GetString());
            Assert.Equal(key, receipt.GetProperty(RequestKeyField).GetString());
            var job = receipt.GetProperty(JobField);
            jobId ??= job.GetProperty(IdField).GetString();
            Assert.Equal(jobId, job.GetProperty(IdField).GetString());
            Assert.Equal(job.GetProperty(VersionField).GetString(), accepted.Headers.ETag!.Tag);
            Assert.Equal(path + Grants + jobId, accepted.Headers.Location!.OriginalString);
            using var details = await test.CatalogueAsync(HttpMethod.Get, path, direct: direct);
            await RequireTerminalAsync(details, id);
        }
        await using var scope = test.Scope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Single(await database.Set<ApplicationGrantJob>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.Single(await database.Set<ApplicationGrantApproval>().AsNoTracking().ToArrayAsync(TestContext.Current.CancellationToken));
        Assert.False((await database.Set<ApplicationPolicy>().AsNoTracking().SingleAsync(row => row.ApplicationId == id, TestContext.Current.CancellationToken)).Enabled);
    }

    /// <summary>Retains managed metadata and application version for recovery while refusing ordinary editor authority.</summary>
    private static async Task RequireTerminalAsync(HttpResponseMessage response, string id)
    {
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(ApplicationVersion.Create(id, TerminalRevision), response.Headers.ETag!.Tag);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(TestContext.Current.CancellationToken));
        Assert.True(json.RootElement.GetProperty(RetiredField).GetBoolean());
        Assert.False(json.RootElement.GetProperty(EditableField).GetBoolean());
        Assert.False(json.RootElement.GetProperty(EnabledField).GetBoolean());
        Assert.Equal(Profile, json.RootElement.GetProperty(ProfileField).GetString());
    }
}
