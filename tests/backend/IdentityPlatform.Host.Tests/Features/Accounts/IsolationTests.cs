using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using IdentityPlatform.Core.Features.Configuration;
using IdentityPlatform.Infrastructure.Persistence;
using Microsoft.AspNetCore.Http;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Verifies administrator-only diagnostics and safe database boundaries independently from pilot activation.</summary>
public sealed class IsolationTests
{
    private const string PostgreSql = "Host=localhost;Database=synthetic";
    private const string SqlServer = "Server=localhost;Database=synthetic;TrustServerCertificate=false";
    private const int BoundSeconds = 5;
    private const string GuidFormat = "N";
    private static readonly string[] SensitiveCategories = ["OpenIddict.Server", "Microsoft.AspNetCore.Hosting.Diagnostics", "Microsoft.EntityFrameworkCore.Database.Command"];
    private const string IncidentHeader = "X-Identity-Incident";

    /// <summary>Checks native provider options without claiming a live database or lock-cancellation test.</summary>
    [Theory]
    [InlineData(DatabaseProvider.PostgreSql)]
    [InlineData(DatabaseProvider.SqlServer)]
    public void Given_AccountDatabase_When_BoundsAreApplied_Then_NativeConnectionAndCommandTimeoutsAreLimited(DatabaseProvider provider)
    {
        var services = new ServiceCollection().AddLogging();
        services.AddIdentityPersistence(provider, provider == DatabaseProvider.PostgreSql ? PostgreSql : SqlServer);
        using var container = services.BuildServiceProvider();
        using var scope = container.CreateScope();
        var database = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        AccountDatabaseBounds.Apply(database);
        Assert.Equal(BoundSeconds, database.Database.GetCommandTimeout());
        var connection = database.Database.GetConnectionString();
        Assert.Equal(BoundSeconds, provider == DatabaseProvider.PostgreSql
            ? new NpgsqlConnectionStringBuilder(connection).Timeout
            : new SqlConnectionStringBuilder(connection).ConnectTimeout);
    }

    /// <summary>Ensures failures resolving the database are covered by the same safe boundary as endpoint failures.</summary>
    [Fact]
    public async Task Given_MissingDatabase_When_RequestGuardRuns_Then_FailureIsSafeAndEndpointIsNotEntered()
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        await using var body = new MemoryStream();
        var context = new DefaultHttpContext { RequestServices = services };
        context.Request.Path = AccountHost.Probe;
        context.Response.Body = body;
        var entered = false;
        var incidents = new List<string>();
        await AccountRequestGuard.InvokeAsync(context, _ => { entered = true; return Task.CompletedTask; }, incidents.Add);
        Assert.False(entered);
        Assert.Equal(StatusCodes.Status503ServiceUnavailable, context.Response.StatusCode);
        Assert.Equal(Assert.Single(incidents), context.Response.Headers[IncidentHeader]);
        Assert.True(Guid.TryParseExact(incidents[0], GuidFormat, out _));
        Assert.Equal("no-store", context.Response.Headers.CacheControl);
        Assert.Equal(0, context.Response.Headers.SetCookie.Count);
        Assert.Equal("Sign-in is temporarily unavailable. Return to your application and try again.", System.Text.Encoding.UTF8.GetString(body.ToArray()));
    }

    /// <summary>Tests the actual administrator-only host logger filters before and after native options recreation.</summary>
    [Fact]
    public async Task Given_AdministratorOnly_When_LoggingOptionsAreRecreated_Then_SensitiveCategoriesRemainDisabled()
    {
        await using var host = await AccountHost.StartAsync(false, true);
        var factory = host.Services.GetRequiredService<ILoggerFactory>();
        var cache = host.Services.GetRequiredService<IOptionsMonitorCache<LoggerFilterOptions>>();
        var monitor = host.Services.GetRequiredService<IOptionsMonitor<LoggerFilterOptions>>();
        var categories = SensitiveCategories;
        foreach (var category in categories)
        {
            Assert.False(factory.CreateLogger(category).IsEnabled(LogLevel.Critical));
        }
        cache.TryRemove(Options.DefaultName);
        var refreshed = monitor.CurrentValue;
        Assert.All(categories, category => Assert.Contains(refreshed.Rules,
            rule => category.StartsWith(rule.CategoryName ?? string.Empty, StringComparison.Ordinal) && rule.LogLevel == LogLevel.None));
    }
}
