using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using IdentityPlatform.Database.Hosting;

namespace IdentityPlatform.Database.SqlServer.Hosting;

/// <summary>Supplies the pinned existing SqlServer container and fixed logical stores for local Aspire execution.</summary>
/// <example><code>var adapters = new DatabaseHostingProviders(new SqlServerDatabaseHostingProvider());</code></example>
public sealed class SqlServerDatabaseHostingProvider : IDatabaseHostingProvider
{
    private const string DatabaseName = "database";
    private const string PlatformName = "platform";
    private const string SessionsName = "adminsessions";
    private const string TcpEndpoint = "tcp";
    private const string ImageTag = "2022-latest";
    private const string ImageDigest = "4402d880dd4c34bfa7d8705e56a86cd6c88da80a1f6bbbe741f999e76264a090";
    private const string ServerKey = "Server=";
    private const string PlatformCredentials = ";Database=platform;User ID=platform_runtime;Password=";
    private const string SessionsCredentials = ";Database=adminsessions;User ID=sessions_runtime;Password=";
    private const string LocalTransport = ";Encrypt=True;TrustServerCertificate=True";

    public IRelationalDatabaseProvider Provider { get; } = new SqlServerDatabaseProvider();

    /// <summary>Retains resource names, image digest and caller-owned generated secret parameters.</summary>
    /// <example><code>var resources = adapter.Add(builder, platformPassword, sessionsPassword);</code></example>
    public DatabaseResources Add(IDistributedApplicationBuilder builder, IResourceBuilder<ParameterResource> platformPassword,
        IResourceBuilder<ParameterResource> sessionsPassword)
    {
        ArgumentNullException.ThrowIfNull(builder);
        var database = builder.AddSqlServer(DatabaseName)
            .WithImageTag(ImageTag)
            .WithImageSHA256(ImageDigest);
        var platform = database.AddDatabase(PlatformName);
        var sessions = database.AddDatabase(SessionsName);
        var endpoint = database.GetEndpoint(TcpEndpoint);
        // The disposable local SQL container uses its self-signed transport certificate.
        // This does not alter HTTPS certificate/issuer validation for application traffic.
        var platformRuntime = ReferenceExpression.Create($"{ServerKey}{endpoint.Property(EndpointProperty.Host)},{endpoint.Property(EndpointProperty.Port)}{PlatformCredentials}{platformPassword}{LocalTransport}");
        var sessionsRuntime = ReferenceExpression.Create($"{ServerKey}{endpoint.Property(EndpointProperty.Host)},{endpoint.Property(EndpointProperty.Port)}{SessionsCredentials}{sessionsPassword}{LocalTransport}");
        return new DatabaseResources(Provider, platformPassword, sessionsPassword, platform, sessions, platformRuntime, sessionsRuntime);
    }
}
