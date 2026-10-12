using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IdentityPlatform.CodeQuality.Features.Evidence;

/// <summary>Limit generator exemptions to native EF artifact shapes, never arbitrary files in migration directories.</summary>
internal static class GeneratedArtifacts
{
    private static readonly string[] Roots =
    [
        "src/migrations/IdentityPlatform.Migrations.PostgreSql/Platform/",
        "src/migrations/IdentityPlatform.Migrations.PostgreSql/Sessions/",
        "src/migrations/IdentityPlatform.Migrations.SqlServer/Platform/",
        "src/migrations/IdentityPlatform.Migrations.SqlServer/Sessions/"
    ];
    private static readonly string[] Snapshots = ["PlatformDbContextModelSnapshot", "AdminSessionDbContextModelSnapshot"];
    private static readonly string[] MigrationMethods = ["Up", "Down"];
    private const string SnapshotBase = "ModelSnapshot";
    private const string MigrationBase = "Migration";
    private const string BuildSnapshot = "BuildModel";
    private const string BuildMigration = "BuildTargetModel";
    private const string DesignerSuffix = ".Designer";
    private const int TimestampLength = 14;

    /// <summary>Require the expected location, class identity, base and complete native method inventory.</summary>
    internal static bool Matches(string path, string source)
    {
        if (!Roots.Any(root => path.StartsWith(root, StringComparison.Ordinal)))
        {
            return false;
        }

        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        if (root.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error) ||
            root.DescendantTrivia(descendIntoTrivia: true).Any(trivia => trivia.IsKind(SyntaxKind.IfDirectiveTrivia)))
        {
            return false;
        }
        var types = root.DescendantNodes().OfType<TypeDeclarationSyntax>().ToArray();
        if (types.Length != 1 || types[0] is not ClassDeclarationSyntax type)
        {
            return false;
        }

        var name = System.IO.Path.GetFileNameWithoutExtension(path);
        if (Snapshots.Contains(name))
        {
            return type.Identifier.ValueText == name && HasBase(type, SnapshotBase) && HasMethods(type, [BuildSnapshot]);
        }
        var designer = name.EndsWith(DesignerSuffix, StringComparison.Ordinal);
        if (designer)
        {
            name = name[..^DesignerSuffix.Length];
        }

        if (name.Length <= TimestampLength || name[TimestampLength] != '_' || !name[..TimestampLength].All(char.IsAsciiDigit))
        {
            return false;
        }

        if (type.Identifier.ValueText != name[(TimestampLength + 1)..])
        {
            return false;
        }

        return designer
            ? type.Modifiers.Any(SyntaxKind.PartialKeyword) && HasMethods(type, [BuildMigration])
            : HasBase(type, MigrationBase) && HasMethods(type, MigrationMethods);
    }

    /// <summary>Check the native generator's simple base name; no source attribute alone grants an exemption.</summary>
    private static bool HasBase(ClassDeclarationSyntax type, string expected) =>
        type.BaseList?.Types.Any(value => value.Type.ToString() == expected) == true;

    /// <summary>Exclude helper members or extra executable declarations from the accepted generated shape.</summary>
    private static bool HasMethods(ClassDeclarationSyntax type, string[] expected) =>
        type.Members.All(member => member is MethodDeclarationSyntax) &&
        type.Members.OfType<MethodDeclarationSyntax>().Select(method => method.Identifier.ValueText)
            .Order(StringComparer.Ordinal).SequenceEqual(expected.Order(StringComparer.Ordinal));
}
