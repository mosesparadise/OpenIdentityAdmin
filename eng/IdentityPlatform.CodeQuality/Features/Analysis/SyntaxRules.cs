using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IdentityPlatform.CodeQuality.Features.Analysis;

/// <summary>Apply repository rules to parsed declarations, never to text inside comments or strings.</summary>
internal static class SyntaxRules
{
    internal const string ScenarioRule = "CQ001";
    internal const string MethodDocumentationRule = "CQ002";
    internal const string PublicDocumentationRule = "CQ003";
    internal const string MethodSizeRule = "CQ004";
    internal const string TypeSizeRule = "CQ005";
    internal const string LiteralRule = "CQ006";
    internal const string ConditionalRule = "CQ007";
    internal const string PartialRule = "CQ008";
    internal const int MethodLimit = 40;
    internal const int TypeLimit = 300;
    private static readonly string[] TestAttributeSuffixes = ["Fact", "FactAttribute", "Theory", "TheoryAttribute"];
    private const string GivenPrefix = "Given_";
    private const string WhenSeparator = "_When_";
    private const string ThenSeparator = "_Then_";

    /// <summary>Return exact findings for a single handwritten C# file; invalid syntax fails the scan.</summary>
    internal static IReadOnlyList<Finding> Inspect(string path, string source)
    {
        var root = CSharpSyntaxTree.ParseText(source).GetRoot();
        if (root.GetDiagnostics().Any(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error))
        {
            throw new InvalidDataException(path);
        }
        var findings = new List<Finding>();
        foreach (var node in root.DescendantNodes().Where(IsDeclaration))
        {
            InspectDeclaration(path, node, findings);
        }
        if (root.DescendantTrivia(descendIntoTrivia: true).Any(trivia => trivia.IsKind(SyntaxKind.IfDirectiveTrivia)))
        {
            findings.Add(Finding.Create(ConditionalRule, path, root));
        }
        return findings.Distinct().ToArray();
    }

    /// <summary>Check each declaration with bounded, independent rules and report only metadata.</summary>
    private static void InspectDeclaration(string path, SyntaxNode node, List<Finding> findings)
    {
        // Attach a violation to the declaration currently being inspected.
        void Add(string rule) => findings.Add(Finding.Create(rule, path, node));
        if (node is MethodDeclarationSyntax method && (IsTest(method) || IsAttributedTestCandidate(path, method)) && !IsScenario(method.Identifier.ValueText))
        {
            Add(ScenarioRule);
        }

        if (IsMethod(node) && !DocumentationRules.HasContract(node))
        {
            Add(MethodDocumentationRule);
        }

        if (DocumentationRules.IsExternal(path, node) && !DocumentationRules.HasExample(node))
        {
            Add(PublicDocumentationRule);
        }

        if (IsMethod(node) && LogicalLines(node) > MethodLimit)
        {
            Add(MethodSizeRule);
        }

        if (node is TypeDeclarationSyntax && LogicalLines(node) > TypeLimit)
        {
            Add(TypeSizeRule);
        }

        if (node is TypeDeclarationSyntax declaration && declaration.Modifiers.Any(SyntaxKind.PartialKeyword))
        {
            Add(PartialRule);
        }

        if (LiteralRules.HasUnexplainedValue(node))
        {
            Add(LiteralRule);
        }
    }

    /// <summary>Require naming for public attributed test methods, including custom or globally aliased test attributes.</summary>
    private static bool IsAttributedTestCandidate(string path, MethodDeclarationSyntax method)
    {
        const string TestRoot = "tests/";
        return path.StartsWith(TestRoot, StringComparison.Ordinal) && method.AttributeLists.Count > 0 &&
            method.Modifiers.Any(SyntaxKind.PublicKeyword);
    }

    /// <summary>Recognize scenario components without accepting empty or repeated delimiters.</summary>
    internal static bool IsScenario(string name)
    {
        var when = name.IndexOf(WhenSeparator, StringComparison.Ordinal);
        var then = name.IndexOf(ThenSeparator, StringComparison.Ordinal);
        return name.StartsWith(GivenPrefix, StringComparison.Ordinal) && when > GivenPrefix.Length &&
            then > when + WhenSeparator.Length && then + ThenSeparator.Length < name.Length &&
            name.LastIndexOf(WhenSeparator, StringComparison.Ordinal) == when &&
            name.LastIndexOf(ThenSeparator, StringComparison.Ordinal) == then;
    }

    /// <summary>Recognize xUnit facts/theories, including qualified and aliased attribute names.</summary>
    internal static bool IsTest(MethodDeclarationSyntax method)
    {
        var root = (CompilationUnitSyntax)method.SyntaxTree.GetRoot();
        var aliases = root.Usings.Concat(method.Ancestors().OfType<BaseNamespaceDeclarationSyntax>()
                .Reverse().SelectMany(scope => scope.Usings))
            .Where(directive => directive.Alias is not null)
            .GroupBy(directive => directive.Alias!.Name.Identifier.ValueText, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Last().Name!.ToString(), StringComparer.Ordinal);
        return method.AttributeLists.SelectMany(list => list.Attributes).Any(attribute =>
        {
            var name = attribute.Name.ToString();
            name = aliases.GetValueOrDefault(name, name).Split('.').Last();
            return TestAttributeSuffixes.Any(suffix => name.EndsWith(suffix, StringComparison.Ordinal));
        });
    }

    /// <summary>Count distinct code-bearing lines, excluding braces and comments; never count generated documentation.</summary>
    internal static int LogicalLines(SyntaxNode node) => node.DescendantTokens()
        .Where(token => !token.IsKind(SyntaxKind.OpenBraceToken) && !token.IsKind(SyntaxKind.CloseBraceToken))
        .SelectMany(token => Enumerable.Range(token.GetLocation().GetLineSpan().StartLinePosition.Line,
            token.GetLocation().GetLineSpan().EndLinePosition.Line - token.GetLocation().GetLineSpan().StartLinePosition.Line + 1))
        .Distinct().Count();

    /// <summary>Include bodies on methods, constructors, operators, local functions and explicit accessors.</summary>
    internal static bool IsMethod(SyntaxNode node) => node is BaseMethodDeclarationSyntax or LocalFunctionStatementSyntax ||
        node is AccessorDeclarationSyntax { Body: not null } or AccessorDeclarationSyntax { ExpressionBody: not null };

    /// <summary>Select declaration owners, avoiding duplicate literal findings on outer types.</summary>
    private static bool IsDeclaration(SyntaxNode node) => IsMethod(node) || node is TypeDeclarationSyntax or FieldDeclarationSyntax or PropertyDeclarationSyntax;
}
