using System.Text.Json;
using IdentityPlatform.Core.Features.Catalogue;

namespace IdentityPlatform.Core.Tests.Features.Catalogue;

/// <summary>Independently checks the exact public disclosure shape and actual Boolean verification without any raw identity-principal copying.</summary>
public sealed class ConsentedIdentityTests
{
    private const int SubjectLimit = 450;
    private const int SubjectOverflow = 451;
    private const int EmailLimit = 256;
    private const int EmailOverflow = 257;

    private const string NoDisclosure = "none";
    private const string EmailDisclosure = "email";
    private const string UnsupportedDisclosure = "roles";
    private const string SubjectClaim = "sub";
    private const string VerificationClaim = "email_verified";
    private const string ControlEmail = "line\n@example.test";
    private const string MalformedEmail = "broken\ud800@example.test";

    private const string Subject = "opaque-subject";
    private const string Email = "Person@example.test";

    /// <summary>Subject-only policy and absent email both omit the whole email pair regardless of the stored verification flag.</summary>
    [Theory]
    [InlineData(NoDisclosure, Email, true)]
    [InlineData(EmailDisclosure, null, true)]
    [InlineData(EmailDisclosure, "", false)]
    public void Given_NoDisclosureOrMissingEmail_When_ProfileSerialized_Then_OnlyExactSubjectIsPresent(string disclosure, string? email, bool verified)
    {
        var profile = ConsentedIdentity.Create(Subject, email, verified, disclosure);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(profile));
        Assert.Equal([SubjectClaim], document.RootElement.EnumerateObject().Select(value => value.Name));
        Assert.Equal(Subject, document.RootElement.GetProperty(SubjectClaim).GetString());
    }

    /// <summary>Consented current email preserves its exact Identity spelling and emits true/false as JSON Boolean, never a string.</summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Given_ConsentedCurrentEmail_When_ProfileSerialized_Then_OnlyExactPairAndSubjectArePresent(bool verified)
    {
        var profile = ConsentedIdentity.Create(Subject, Email, verified, EmailDisclosure);
        using var document = JsonDocument.Parse(JsonSerializer.Serialize(profile));
        Assert.Equal([EmailDisclosure, VerificationClaim, SubjectClaim], document.RootElement.EnumerateObject().Select(value => value.Name).Order(StringComparer.Ordinal));
        Assert.Equal(Email, document.RootElement.GetProperty(EmailDisclosure).GetString());
        Assert.Equal(verified, document.RootElement.GetProperty(VerificationClaim).GetBoolean());
        Assert.Equal(Subject, document.RootElement.GetProperty(SubjectClaim).GetString());
    }

    /// <summary>Bad current source values cannot be normalized, truncated or converted into an arbitrary claim set.</summary>
    [Fact]
    public void Given_UnsupportedDisclosureOrMalformedSource_When_Projecting_Then_NoPublicProfileIsProduced()
    {
        Assert.Throws<ArgumentException>(() => ConsentedIdentity.Create(Subject, Email, true, UnsupportedDisclosure));
        Assert.Throws<ArgumentException>(() => ConsentedIdentity.Create("", Email, true, EmailDisclosure));
        Assert.Throws<ArgumentException>(() => ConsentedIdentity.Create(Subject, ControlEmail, true, EmailDisclosure));
        Assert.Throws<ArgumentException>(() => ConsentedIdentity.Create(Subject, MalformedEmail, true, EmailDisclosure));
        Assert.Throws<ArgumentException>(() => ConsentedIdentity.Create(Subject, new string('x', EmailOverflow), true, EmailDisclosure));
        Assert.Equal(new string('s', SubjectLimit), ConsentedIdentity.Create(new string('s', SubjectLimit), null, false, NoDisclosure).Subject);
        Assert.Throws<ArgumentException>(() => ConsentedIdentity.Create(new string('s', SubjectOverflow), null, false, NoDisclosure));
        Assert.Equal(new string('x', EmailLimit), ConsentedIdentity.Create(Subject, new string('x', EmailLimit), false, EmailDisclosure).Email);
    }
}
