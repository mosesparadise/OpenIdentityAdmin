using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using IdentityPlatform.Database.Hosting;

namespace IdentityPlatform.Database.PostgreSql.Hosting;

/// <summary>Supplies the pinned existing PostgreSql container and fixed logical stores for local Aspire execution.</summary>
/// <example><code>var adapters = new DatabaseHostingProviders(new PostgreSqlDatabaseHostingProvider());</code></example>
public sealed class PostgreSqlDatabaseHostingProvider : IDatabaseHostingProvider
{
    private const string DatabaseName = "database";
    private const string PlatformName = "platform";
    private const string SessionsName = "adminsessions";
    private const string TcpEndpoint = "tcp";
    private const string ImageTag = "18";
    private const string ImageDigest = "74935e72241653ca55e0414067e6d8763aceb8a810eb51b452253ec3dcfc4336";
    private const string HostKey = "Host=";
    private const string PortKey = ";Port=";
    private const string PlatformCredentials = ";Database=platform;Username=platform_runtime;Password=";
    private const string SessionsCredentials = ";Database=adminsessions;Username=sessions_runtime;Password=";

    public IRelationalDatabaseProvider Provider { get; } = new PostgreSqlDatabaseProvider();

    /// <summary>Retains resource names, image digest and caller-owned generated secret parameters.</summary>
    /// <example><code>var resources = adapter.Add(builder, platformPassword, sessionsPassword);</code></example>
    public DatabaseResources Add(IDistributedApplicationBuilder builder, IResourceBuilder<ParameterResource> platformPassword,
        IResourceBuilder<ParameterResource> sessionsPassword)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var database = builder.AddPostgres(DatabaseName)
            .WithImageTag(ImageTag)
            .WithImageSHA256(ImageDigest);
        var platform = database.AddDatabase(PlatformName);
        var sessions = database.AddDatabase(SessionsName);
        var endpoint = database.GetEndpoint(TcpEndpoint);
        var platformRuntime = ReferenceExpression.Create($"{HostKey}{endpoint.Property(EndpointProperty.Host)}{PortKey}{endpoint.Property(EndpointProperty.Port)}{PlatformCredentials}{platformPassword}");
        var sessionsRuntime = ReferenceExpression.Create($"{HostKey}{endpoint.Property(EndpointProperty.Host)}{PortKey}{endpoint.Property(EndpointProperty.Port)}{SessionsCredentials}{sessionsPassword}");
        return new DatabaseResources(Provider, platformPassword, sessionsPassword, platform, sessions, platformRuntime, sessionsRuntime);
    }
}
