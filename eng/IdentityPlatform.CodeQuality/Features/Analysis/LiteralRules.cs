using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace IdentityPlatform.CodeQuality.Features.Analysis;

/// <summary>Find unnamed literal candidates while preserving independently authored test expectations.</summary>
internal static class LiteralRules
{
    private const string AssertType = "Assert";
    private const string ExpectedParameter = "expected";
    private const string EqualityAssertion = "Equal";
    private const string PrefixAssertion = "StartsWith";
    private const string SuffixAssertion = "EndsWith";
    private const string ContainsAssertion = "Contains";
    private const string DoesNotContainAssertion = "DoesNotContain";
    private const string MatchesAssertion = "Matches";
    private const string DoesNotMatchAssertion = "DoesNotMatch";

    /// <summary>Report one actionable finding per declaration rather than echoing source values.</summary>
    internal static bool HasUnexplainedValue(SyntaxNode node)
    {
        if (node is TypeDeclarationSyntax)
        {
            return false;
        }

        return node.DescendantNodes(descendIntoChildren: child => child == node ||
                child is not BaseMethodDeclarationSyntax and not LocalFunctionStatementSyntax and not AccessorDeclarationSyntax)
            .Any(value => IsCandidate(value) && !HasPurpose(value) && !IsExpectedTestValue(value));
    }

    /// <summary>Allow only intrinsic empty/zero/unit literals; other strings and numeric values require context.</summary>
    private static bool IsCandidate(SyntaxNode value) => value switch
    {
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.StringLiteralExpression) || literal.IsKind(SyntaxKind.Utf8StringLiteralExpression) => literal.Token.ValueText.Length > 0,
        LiteralExpressionSyntax literal when literal.IsKind(SyntaxKind.NumericLiteralExpression) =>
            IsNontrivialNumber(literal.Token.Value),
        InterpolatedStringTextSyntax text => text.TextToken.ValueText.Any(char.IsLetterOrDigit),
        _ => false
    };

    /// <summary>Compare in the literal's actual numeric type without overflow or decimal-to-double precision loss.</summary>
    private static bool IsNontrivialNumber(object? value) => value switch
    {
        int number => number is not 0 and not 1,
        uint number => number is not 0 and not 1,
        long number => number is not 0 and not 1,
        ulong number => number is not 0 and not 1,
        float number => number is not 0 and not 1,
        double number => number is not 0 and not 1,
        decimal number => number is not 0 and not 1,
        _ => true
    };

    /// <summary>Recognize named constants/read-only fields and declarative attribute data as explicit value ownership.</summary>
    private static bool HasPurpose(SyntaxNode value)
    {
        if (value.Ancestors().Any(parent => parent is AttributeSyntax))
        {
            return true;
        }

        var field = value.Ancestors().OfType<FieldDeclarationSyntax>().FirstOrDefault();
        if (field is not null && (field.Modifiers.Any(SyntaxKind.ConstKeyword) || field.Modifiers.Any(SyntaxKind.ReadOnlyKeyword)))
        {
            return true;
        }

        return value.Ancestors().OfType<LocalDeclarationStatementSyntax>().Any(local => local.Modifiers.Any(SyntaxKind.ConstKeyword));
    }

    /// <summary>Permit independently specified expected arguments only inside syntactically identified test methods.</summary>
    private static bool IsExpectedTestValue(SyntaxNode value)
    {
        var method = value.Ancestors().OfType<MethodDeclarationSyntax>().FirstOrDefault();
        if (method is null || !SyntaxRules.IsTest(method))
        {
            return false;
        }

        var argument = value.Ancestors().OfType<ArgumentSyntax>().FirstOrDefault();
        if (argument?.Parent is not ArgumentListSyntax list || list.Parent is not InvocationExpressionSyntax invocation ||
            invocation.Expression is not MemberAccessExpressionSyntax access || access.Expression.ToString() != AssertType)
        {
            return false;
        }

        var name = access.Name.Identifier.ValueText;
        var expected = argument.NameColon?.Name.Identifier.ValueText == ExpectedParameter ||
            argument.NameColon is null && list.Arguments.IndexOf(argument) == 0;
        return expected && name is EqualityAssertion or PrefixAssertion or SuffixAssertion or ContainsAssertion or
            DoesNotContainAssertion or MatchesAssertion or DoesNotMatchAssertion;
    }
}
