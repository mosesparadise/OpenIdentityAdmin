using IdentityPlatform.CodeQuality.Features.Analysis;

namespace IdentityPlatform.CodeQuality.Features.Evidence;

/// <summary>Scan source roots with narrow generator exclusions; a GeneratedCode attribute cannot suppress checks.</summary>
internal static class RepositoryScan
{
    private static readonly string[] SourceRoots = ["src", "tests", "eng"];
    private static readonly string[] BuildDirectories = ["bin", "obj", "node_modules"];
    private const string CSharpPattern = "*.cs";

    /// <summary>Read all handwritten C# files, including untracked candidate files, with deterministic ordering.</summary>
    internal static IReadOnlyList<Finding> Read(string root)
    {
        var paths = SourceRoots.Select(folder => Path.Combine(root, folder)).Where(Directory.Exists)
            .SelectMany(folder => Directory.EnumerateFiles(folder, CSharpPattern, SearchOption.AllDirectories))
            .Where(path => !path.Split(Path.DirectorySeparatorChar).Intersect(BuildDirectories).Any())
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal).ToArray();
        return paths.SelectMany(path =>
        {
            var source = File.ReadAllText(Path.Combine(root, path));
            return GeneratedArtifacts.Matches(path, source) ? [] : SyntaxRules.Inspect(path, source);
        }).ToArray();
    }
}
