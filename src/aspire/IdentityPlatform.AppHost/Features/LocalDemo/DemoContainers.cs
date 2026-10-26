namespace IdentityPlatform.AppHost.Features.LocalDemo;

/// <summary>Uses pinned build stages, non-root Linux storage and ordinary TLS trust for the local runtime containers.</summary>
internal static class DemoContainers
{
    private const string ContainerPrefix = "oid-demo-";
    private const string NameSeparator = "-";
    private const string LabelArgument = "--label=";
    private const string LabelAssignment = "=";
    private const string DotnetEnvironment = "DOTNET_ENVIRONMENT";
    private const string AspnetEnvironment = "ASPNETCORE_ENVIRONMENT";
    private const string Development = "Development";
    private const string HttpsEndpoint = "https";
    private const string LoopbackAddress = "127.0.0.1";
    private const string UrlsVariable = "ASPNETCORE_URLS";
    private const string ListenPrefix = "https://+:";
    private const string EnabledVariable = "LocalDemo__Enabled";
    private const string EnabledValue = "true";
    private const string TmpfsArgument = "--tmpfs=";
    private const string CertificatePathVariable = "Kestrel__Certificates__Default__Path";
    private const string CertificateKeyVariable = "Kestrel__Certificates__Default__KeyPath";
    private const string TrustDirectoryVariable = "SSL_CERT_DIR";
    private const string HealthPath = "/health";
    internal const string RunLabel = "openidentityadmin.local-demo.run";
    internal const string RoleLabel = "openidentityadmin.local-demo.role";
    private const string Dockerfile = "scripts/local-demo/Dockerfile";
    private const string RepositoryRoot = "../../..";
    private const string PrivateMount = "/run/identity-demo:rw,noexec,nosuid,nodev,size=4m,mode=0700,uid=1654,gid=1654";

    /// <summary>Creates a disposable runtime image using the existing project output and a single run ownership label.</summary>
    internal static IResourceBuilder<ContainerResource> AddDemoContainer(this IDistributedApplicationBuilder builder, string name, string stage, string run) =>
        builder.AddDockerfile(name, RepositoryRoot, Dockerfile, stage)
            .WithContainerName(ContainerPrefix + run + NameSeparator + name)
            .WithContainerRuntimeArgs(LabelArgument + RunLabel + LabelAssignment + run, LabelArgument + RoleLabel + LabelAssignment + name)
            .WithEnvironment(DotnetEnvironment, Development)
            .WithEnvironment(AspnetEnvironment, Development);

    /// <summary>Aligns host/target ports and DNS aliases with native identities; Kestrel receives the native certificate providers directly.</summary>
#pragma warning disable ASPIRECERTIFICATES001 // Pinned 13.6.1 Development-only API; re-review on Aspire upgrade (feature 014).
    internal static IResourceBuilder<ContainerResource> WithDemoHttps(this IResourceBuilder<ContainerResource> resource, string host, int port) => resource
        .WithHttpsEndpoint(port: port, targetPort: port, name: HttpsEndpoint, isProxied: false)
        .WithEndpoint(HttpsEndpoint, endpoint => endpoint.TargetHost = LoopbackAddress)
        .WithContainerNetworkAlias(host)
        .WithEnvironment(UrlsVariable, ListenPrefix + port.ToString(System.Globalization.CultureInfo.InvariantCulture))
        .WithEnvironment(EnabledVariable, EnabledValue)
        .WithContainerRuntimeArgs(TmpfsArgument + PrivateMount)
        .WithHttpsDeveloperCertificate()
        .WithHttpsCertificateConfiguration(context =>
        {
            context.EnvironmentVariables[CertificatePathVariable] = context.CertificatePath;
            context.EnvironmentVariables[CertificateKeyVariable] = context.KeyPath;
            return Task.CompletedTask;
        })
        .WithDeveloperCertificateTrust(true)
        .WithCertificateTrustScope(CertificateTrustScope.Append)
        .WithCertificateTrustConfiguration(context =>
        {
            context.EnvironmentVariables[TrustDirectoryVariable] = context.CertificateDirectoriesPath;
            return Task.CompletedTask;
        })
        .WithHttpHealthCheck(HealthPath, endpointName: HttpsEndpoint);
#pragma warning restore ASPIRECERTIFICATES001
}
