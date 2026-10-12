using System.Xml;
using System.Xml.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IdentityPlatform.CodeQuality.Features.Analysis;

/// <summary>Validate XML structure without resolving external resources or treating empty tags as documentation.</summary>
internal static class DocumentationRules
{
    private const string SummaryElement = "summary";
    private const string ExampleElement = "example";
    private const string XmlRootStart = "<documentation>";
    private const string XmlRootEnd = "</documentation>";
    private const string CommentPrefix = "///";
    private const string TestRoot = "tests/";

    /// <summary>Require a nonempty purpose summary; reviewers separately assess contract accuracy and completeness.</summary>
    internal static bool HasContract(SyntaxNode node) => node is LocalFunctionStatementSyntax
        ? node.GetLeadingTrivia().Any(trivia => trivia.IsKind(SyntaxKind.SingleLineCommentTrivia) && trivia.ToString().Length > CommentPrefix.Length)
        : HasContent(Read(node), SummaryElement);

    /// <summary>Require actual public usage content, including code examples, instead of empty example elements.</summary>
    internal static bool HasExample(SyntaxNode node)
    {
        var document = Read(node);
        return HasContent(document, SummaryElement) && HasContent(document, ExampleElement);
    }

    /// <summary>Identify public declarations whose entire containing type chain is public; tests are not external APIs.</summary>
    internal static bool IsExternal(string path, SyntaxNode node)
    {
        if (node is not TypeDeclarationSyntax and not BaseMethodDeclarationSyntax || path.StartsWith(TestRoot, StringComparison.Ordinal))
        {
            return false;
        }

        var declarations = node.AncestorsAndSelf().OfType<MemberDeclarationSyntax>().Where(member => member is TypeDeclarationSyntax or BaseMethodDeclarationSyntax);
        return declarations.Any() && declarations.All(IsVisible);
    }

    /// <summary>Include externally overridable members and implicit public interface contracts without exposing private-protected members.</summary>
    private static bool IsVisible(MemberDeclarationSyntax member) => member.Modifiers.Any(SyntaxKind.PublicKeyword) ||
        member.Modifiers.Any(SyntaxKind.ProtectedKeyword) && !member.Modifiers.Any(SyntaxKind.PrivateKeyword) ||
        member.Parent is InterfaceDeclarationSyntax && !member.Modifiers.Any(SyntaxKind.PrivateKeyword);

    /// <summary>Parse only leading XML documentation with DTD and external entity resolution disabled.</summary>
    private static XDocument? Read(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia().Where(value => value.IsKind(SyntaxKind.SingleLineDocumentationCommentTrivia));
        var text = string.Concat(trivia.Select(value => value.ToFullString()));
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var lines = text.Split('\n').Select(line => line.TrimStart().StartsWith(CommentPrefix, StringComparison.Ordinal)
            ? line.TrimStart()[CommentPrefix.Length..] : line);
        try
        {
            using var reader = XmlReader.Create(new StringReader(XmlRootStart + string.Join('\n', lines) + XmlRootEnd),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            return XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return null;
        }
    }

    /// <summary>Accept nonempty visible XML content; boilerplate usefulness remains a review obligation.</summary>
    private static bool HasContent(XDocument? document, string element) =>
        document?.Root?.Elements(element).Any(value => !string.IsNullOrWhiteSpace(value.Value)) == true;
}
