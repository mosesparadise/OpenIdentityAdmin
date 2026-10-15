using System.Xml.Linq;

namespace IdentityPlatform.Architecture.Tests.Features;

public sealed class DependencyRulesTests
{
    private static readonly string[] AllowedRuntimeReferences =
    [
        "../../persistence/IdentityPlatform.Infrastructure/IdentityPlatform.Infrastructure.csproj",
        "../../aspire/IdentityPlatform.ServiceDefaults/IdentityPlatform.ServiceDefaults.csproj",
        "../../persistence/IdentityPlatform.Database.PostgreSql/IdentityPlatform.Database.PostgreSql.csproj",
        "../../persistence/IdentityPlatform.Database.SqlServer/IdentityPlatform.Database.SqlServer.csproj"
    ];
    private static readonly string[] AllowedInfrastructureReferences = ["../../core/IdentityPlatform.Core/IdentityPlatform.Core.csproj", "../IdentityPlatform.Database.Abstractions/IdentityPlatform.Database.Abstractions.csproj"];
    private static string Repository => FindRepository();

    /// <summary>Keeps the domain assembly independent of every engine, transport and host project.</summary>
    [Fact]
    public void Given_CoreAssembly_When_DependenciesAreInspected_Then_NoProviderOrHostIsReferenced()
    {
        const string PackageReference = "PackageReference";
        const string ProjectReference = "ProjectReference";
        const string FrameworkReference = "FrameworkReference";
        var references = typeof(IdentityPlatform.Core.Features.Configuration.DatabaseProviderParser).Assembly.GetReferencedAssemblies();
        Assert.All(references, reference => Assert.StartsWith("System.", reference.Name));
        const string CoreProject = "src/core/IdentityPlatform.Core/IdentityPlatform.Core.csproj";
        var project = XDocument.Load(Path.Combine(Repository, CoreProject));
        Assert.Empty(project.Descendants(PackageReference));
        Assert.Empty(project.Descendants(ProjectReference));
        Assert.Empty(project.Descendants(FrameworkReference));
    }

    [Fact]
    public void Runtime_hosts_cannot_depend_on_migrations_or_apply_schema_changes()
    {
        foreach (var name in new[] { "AuthServer", "Admin.Api", "Admin.Bff", "Worker" })
        {
            var directory = Path.Combine(Repository, $"src/backend/IdentityPlatform.{name}");
            var project = XDocument.Load(Path.Combine(directory, $"IdentityPlatform.{name}.csproj"));
            var references = project.Descendants("ProjectReference").Select(reference => (string)reference.Attribute("Include")!);
            Assert.All(references, reference => Assert.Contains(reference, AllowedRuntimeReferences));
            foreach (var file in Directory.EnumerateFiles(directory, "*.cs", SearchOption.AllDirectories)
                .Where(file => !file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")))
            {
                var source = File.ReadAllText(file);
                Assert.DoesNotMatch(@"\b(MigrateAsync|Migrate|EnsureCreatedAsync|EnsureCreated)\s*\(", source);
                Assert.DoesNotContain("[ApiController]", source);
                Assert.DoesNotMatch(@":\s*Controller(Base)?\b", source);
            }
        }
    }

    /// <summary>Allows vendor-neutral persistence contracts while retaining host-free shared infrastructure.</summary>
    [Fact]
    public void Given_SharedInfrastructure_When_ProjectReferencesAreInspected_Then_OnlyApprovedContractsRemain()
    {
        const string InfrastructureProject = "src/persistence/IdentityPlatform.Infrastructure/IdentityPlatform.Infrastructure.csproj";
        const string DefaultsProject = "src/aspire/IdentityPlatform.ServiceDefaults/IdentityPlatform.ServiceDefaults.csproj";
        const string ProjectReference = "ProjectReference";
        const string IncludeAttribute = "Include";
        var infrastructure = XDocument.Load(Path.Combine(Repository, InfrastructureProject));
        Assert.All(infrastructure.Descendants(ProjectReference), reference =>
            Assert.Contains((string?)reference.Attribute(IncludeAttribute), AllowedInfrastructureReferences));
        var defaults = XDocument.Load(Path.Combine(Repository, DefaultsProject));
        Assert.Empty(defaults.Descendants(ProjectReference));
    }

    private static string FindRepository()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
        {
            if (File.Exists(Path.Combine(path.FullName, "IdentityPlatform.slnx")))
            {
                return path.FullName;
            }
        }

        throw new InvalidOperationException("Repository root is required for architecture checks.");
    }
}
