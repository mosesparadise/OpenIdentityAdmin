using System.Xml.Linq;
using IdentityPlatform.Core.Features.Configuration;

namespace IdentityPlatform.Architecture.Tests.Features;

public sealed class DependencyRulesTests
{
    private static string Repository => FindRepository();

    [Fact]
    public void Core_has_no_transport_provider_or_host_dependencies()
    {
        var references = typeof(DatabaseProvider).Assembly.GetReferencedAssemblies();
        Assert.All(references, reference => Assert.StartsWith("System.", reference.Name));
        var project = XDocument.Load(Path.Combine(Repository, "src/IdentityPlatform.Core/IdentityPlatform.Core.csproj"));
        Assert.Empty(project.Descendants("PackageReference"));
        Assert.Empty(project.Descendants("ProjectReference"));
        Assert.Empty(project.Descendants("FrameworkReference"));
    }

    [Fact]
    public void Runtime_hosts_cannot_depend_on_migrations_or_apply_schema_changes()
    {
        foreach (var name in new[] { "AuthServer", "Admin.Api", "Admin.Bff", "Worker" })
        {
            var directory = Path.Combine(Repository, $"src/IdentityPlatform.{name}");
            var project = XDocument.Load(Path.Combine(directory, $"IdentityPlatform.{name}.csproj"));
            var references = project.Descendants("ProjectReference").Select(reference => (string)reference.Attribute("Include")!);
            Assert.All(references, reference => Assert.Contains(reference, new[]
            {
                "../IdentityPlatform.Infrastructure/IdentityPlatform.Infrastructure.csproj",
                "../IdentityPlatform.ServiceDefaults/IdentityPlatform.ServiceDefaults.csproj"
            }));
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

    [Fact]
    public void Infrastructure_and_service_defaults_do_not_reference_host_projects()
    {
        var infrastructure = XDocument.Load(Path.Combine(Repository, "src/IdentityPlatform.Infrastructure/IdentityPlatform.Infrastructure.csproj"));
        Assert.All(infrastructure.Descendants("ProjectReference"), reference =>
            Assert.Equal("../IdentityPlatform.Core/IdentityPlatform.Core.csproj", (string?)reference.Attribute("Include")));
        var defaults = XDocument.Load(Path.Combine(Repository, "src/IdentityPlatform.ServiceDefaults/IdentityPlatform.ServiceDefaults.csproj"));
        Assert.Empty(defaults.Descendants("ProjectReference"));
    }

    private static string FindRepository()
    {
        for (var path = new DirectoryInfo(AppContext.BaseDirectory); path is not null; path = path.Parent)
            if (File.Exists(Path.Combine(path.FullName, "IdentityPlatform.slnx"))) return path.FullName;
        throw new InvalidOperationException("Repository root is required for architecture checks.");
    }
}
