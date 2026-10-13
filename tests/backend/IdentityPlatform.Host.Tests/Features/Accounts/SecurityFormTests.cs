using IdentityPlatform.AuthServer.Features.Accounts;
using IdentityPlatform.AuthServer.Features.Accounts.Shared;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;

namespace IdentityPlatform.Host.Tests.Features.Accounts;

/// <summary>Exercises strict native form parsing without substituting authentication or database authority.</summary>
public sealed class SecurityFormTests
{
    private const int MissingToken = 2;
    private const int EmptyLocator = 3;
    private const int ExtraQuery = 4;
    private const int MissingContentType = 5;
    private const string Identifier = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string CodeField = "Code";
    private const string InteractionField = "Interaction";
    private const string TokenField = "__RequestVerificationToken";
    private const string Token = "synthetic-antiforgery-form-shape-only";
    private const string ContentType = "application/x-www-form-urlencoded";
    private const string Extra = "Purpose";
    private const string Query = "?Code=123456";
    private const string Upload = "upload";
    private const string ValidCode = "123456";

    /// <summary>Accepts only exact ASCII canonical codes for the server-selected action.</summary>
    [Theory]
    [InlineData(true, "0123456789ABCDEF0123456789ABCDEF", true)]
    [InlineData(true, "0123456789abcdef0123456789abcdef", false)]
    [InlineData(true, "0123456789ABCDEF0123456789ABCDE", false)]
    [InlineData(true, "0123456789ABCDEF0123456789ABCDEFF", false)]
    [InlineData(true, "G123456789ABCDEF0123456789ABCDEF", false)]
    [InlineData(true, " 123456789ABCDEF0123456789ABCDEF", false)]
    [InlineData(false, "123456", true)]
    [InlineData(false, "12345", false)]
    [InlineData(false, "1234567", false)]
    [InlineData(false, "１２３４５６", false)]
    [InlineData(false, "+12345", false)]
    [InlineData(false, " 12345", false)]
    public async Task Given_ActionSpecificCode_When_CanonicalParserRuns_Then_OnlyExactAsciiShapeIsAccepted(bool recovery, string code, bool accepted)
    {
        var context = Context(code);
        var result = await AdministratorSecurityBrowser.CodeAsync(context, recovery);
        Assert.Equal(accepted, result.HasValue);
        if (accepted)
        {
            Assert.Equal(Identifier, result!.Value.Interaction);
            Assert.Equal(code, result.Value.Code);
        }
    }

    /// <summary>Rejects every ambiguous transport shape before code-dependent processing.</summary>
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    [InlineData(6)]
    public async Task Given_AmbiguousForm_When_SecurityCodeIsParsed_Then_NoCandidateIsReturned(int mutation)
    {
        var context = Context(ValidCode);
        var fields = context.Request.Form.ToDictionary();
        switch (mutation)
        {
            case 0:
                fields[CodeField] = new StringValues([ValidCode, ValidCode]);
                break;
            case 1:
                fields[Extra] = ValidCode;
                break;
            case MissingToken:
                fields.Remove(TokenField);
                break;
            case EmptyLocator:
                fields[InteractionField] = string.Empty;
                break;
            case ExtraQuery:
                context.Request.QueryString = new QueryString(Query);
                break;
            case MissingContentType:
                Assert.Null(await AdministratorSecurityBrowser.CodeAsync(new DefaultHttpContext(), false));
                return;
            default:
                context.Request.Form = new FormCollection(fields, new FormFileCollection { new FormFile(Stream.Null, 0, 0, Upload, Upload) });
                Assert.Null(await AdministratorSecurityBrowser.CodeAsync(context, false));
                return;
        }
        context.Request.Form = new FormCollection(fields);
        Assert.Null(await AdministratorSecurityBrowser.CodeAsync(context, false));
    }

    /// <summary>Unknown enum values cannot become caller-selected native purposes.</summary>
    [Fact]
    public void Given_UnknownSecurityAction_When_PurposeIsSelected_Then_NoNativePurposeIsProduced() =>
        Assert.Throws<ArgumentOutOfRangeException>(() => AdministratorSecurityBrowser.Purpose((AdministratorSecurityAction)int.MaxValue));

    /// <summary>Creates a form-shape control only; actual antiforgery validation remains covered through HTTPS integration tests.</summary>
    private static DefaultHttpContext Context(string code)
    {
        var context = new DefaultHttpContext();
        context.Request.ContentType = ContentType;
        context.Request.Form = new FormCollection(new Dictionary<string, StringValues>
        {
            [CodeField] = code,
            [InteractionField] = Identifier,
            [TokenField] = Token
        });
        return context;
    }
}
