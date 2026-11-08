using System.Text.Json.Serialization;
using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Core.Features.Catalogue;

/// <summary>Projects an already authorized current Identity source into the selected finite public identity fields.</summary>
/// <remarks>This projection does not establish authentication, consent or current authority; the caller must verify all three before publication.</remarks>
/// <example><code>var profile = ConsentedIdentity.Create(subject, currentEmail, emailConfirmed, CatalogueRules.Email);</code></example>
public sealed record ConsentedIdentity
{
    private const int MaximumEmailLength = 256;
    private const int MaximumSubjectLength = 450;
    private const string InvalidSource = "Invalid current identity source.";

    [JsonPropertyName("sub")]
    public string Subject
    {
        get;
    }

    [JsonPropertyName("email"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Email
    {
        get;
    }

    [JsonPropertyName("email_verified"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? EmailVerified
    {
        get;
    }

    /// <summary>Retains only immutable public fields; there is no arbitrary dictionary or administrator-principal copy path.</summary>
    private ConsentedIdentity(string subject, string? email, bool? verified)
    {
        Subject = subject;
        Email = email;
        EmailVerified = verified;
    }

    /// <summary>Omits the complete pair when email is absent or disclosure is not granted; verification is the current Boolean value.</summary>
    /// <example><code>var subjectOnly = ConsentedIdentity.Create(subject, currentEmail, true, CatalogueRules.None);</code></example>
    public static ConsentedIdentity Create(string subject, string? email, bool confirmed, string disclosure)
    {
        if (!ApplicationEditorRules.IsText(subject, MaximumSubjectLength) || disclosure is not CatalogueRules.None and not CatalogueRules.Email)
        {
            throw new ArgumentException(InvalidSource);
        }
        if (disclosure == CatalogueRules.None || string.IsNullOrEmpty(email))
        {
            return new(subject, null, null);
        }
        if (!ApplicationEditorRules.IsText(email, MaximumEmailLength))
        {
            throw new ArgumentException(InvalidSource);
        }
        return new(subject, email, confirmed);
    }
}
