using System.Security.Cryptography;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IdentityPlatform.CodeQuality.Features.Analysis;

/// <summary>Identifies a rule violation without retaining source text or literal values.</summary>
internal sealed record Finding(string Rule, string Path, string Symbol, string Fingerprint)
{
    /// <summary>Bind a finding to its complete syntax and measured size; formatting cannot widen an allowance.</summary>
    internal static Finding Create(string rule, string path, SyntaxNode node)
    {
        var identity = string.Join('.', node.AncestorsAndSelf().Reverse().Select(Name).Where(value => value.Length > 0));
        var context = node switch
        {
            LocalFunctionStatementSyntax => node.Ancestors().OfType<BaseMethodDeclarationSyntax>().FirstOrDefault() as SyntaxNode ?? node.SyntaxTree.GetRoot(),
            AccessorDeclarationSyntax => node.Ancestors().OfType<BasePropertyDeclarationSyntax>().First(),
            _ => node
        };
        var fingerprint = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(context.ToFullString() + node.ToFullString())));
        return new(rule, path, identity, fingerprint);
    }

    /// <summary>Describe declaration identity without serializing default values, bodies or attributes.</summary>
    private static string Name(SyntaxNode node) => node switch
    {
        BaseNamespaceDeclarationSyntax value => value.Name.ToString(),
        TypeDeclarationSyntax value => value.Identifier.ValueText + TypeParameters(value.TypeParameterList),
        MethodDeclarationSyntax value => value.Identifier.ValueText + TypeParameters(value.TypeParameterList) + Parameters(value.ParameterList),
        ConstructorDeclarationSyntax value => value.Identifier.ValueText + Parameters(value.ParameterList),
        LocalFunctionStatementSyntax value => value.Identifier.ValueText + TypeParameters(value.TypeParameterList) + Parameters(value.ParameterList),
        PropertyDeclarationSyntax value => value.Identifier.ValueText,
        AccessorDeclarationSyntax value => value.Keyword.ValueText,
        VariableDeclaratorSyntax value => value.Identifier.ValueText,
        FieldDeclarationSyntax value => string.Join(',', value.Declaration.Variables.Select(variable => variable.Identifier.ValueText)),
        _ => node.KindText()
    };

    /// <summary>Identify generic arity and parameter names without copying attributes or their literal arguments.</summary>
    private static string TypeParameters(TypeParameterListSyntax? list) => list is null ? string.Empty :
        string.Concat('<', string.Join(',', list.Parameters.Select(parameter => parameter.Identifier.ValueText)), '>');

    /// <summary>Include parameter types and modifiers so overloaded method evidence cannot substitute for another method.</summary>
    private static string Parameters(ParameterListSyntax list) =>
        string.Concat('(', string.Join(',', list.Parameters.Select(parameter => parameter.Modifiers.ToString() + parameter.Type)), ')');
}

/// <summary>Provides a stable fallback node description without source-bearing display strings.</summary>
internal static class NodeIdentity
{
    /// <summary>Return an empty description for containers that do not contribute to symbol identity.</summary>
    internal static string KindText(this SyntaxNode node) => node is BaseMethodDeclarationSyntax ? node.GetType().Name : string.Empty;
}
