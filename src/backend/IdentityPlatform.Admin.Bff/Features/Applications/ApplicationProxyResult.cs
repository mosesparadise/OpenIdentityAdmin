using IdentityPlatform.Core.Features.Applications;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Holds only validated finite response data; default diagnostic formatting cannot disclose a one-time credential.</summary>
internal sealed class ApplicationProxyResult(int status, ApplicationDetails? details, ApplicationCredentialOutput? credential, string? version)
{
    private const int SecretBytes = 32;
    private const int RevisionStart = 66;
    internal int Status => status;
    internal ApplicationDetails? Details => details;
    internal ApplicationCredentialOutput? Credential => credential;
    internal string? Version => version;

    /// <summary>Rejects downstream schema, resource and ETag substitution before any response can be published.</summary>
    internal static ApplicationProxyResult Parse(byte[] bytes, string tag, ApplicationMutationRequest mutation, int status)
    {
        ApplicationCredentialOutput? credential = null;
        var details = mutation.Action == ApplicationProxyAction.Edit ? ApplicationJson.Parse<ApplicationDetails>(bytes) :
            (credential = ApplicationJson.Parse<ApplicationCredentialOutput>(bytes)).Application;
        var revision = long.Parse(tag.AsSpan(RevisionStart, tag.Length - RevisionStart - 1), System.Globalization.CultureInfo.InvariantCulture);
        if (!Valid(details) || !ApplicationVersion.Matches(tag, details.Id, revision) || !mutation.Matches(details, revision) ||
            credential is not null && !ValidSecret(credential.ClientSecret))
        {
            throw new InvalidDataException();
        }
        return new(status, details, credential, tag);
    }

    /// <summary>Accepts only the requested resource, validating managed versions or the deliberately empty protected-registration projection.</summary>
    internal static ApplicationProxyResult ParseDetails(byte[] bytes, string id, string? tag)
    {
        var details = ApplicationJson.Parse<ApplicationDetails>(bytes);
        if (details.Id != id || details.ExpiresAt.Offset != TimeSpan.Zero)
        {
            throw new InvalidDataException();
        }
        if (details.Editable)
        {
            if (!Valid(details) || !ApplicationVersion.IsCanonical(tag))
            {
                throw new InvalidDataException();
            }
            var revision = long.Parse(tag!.AsSpan(RevisionStart, tag!.Length - RevisionStart - 1), System.Globalization.CultureInfo.InvariantCulture);
            if (!ApplicationVersion.Matches(tag, id, revision))
            {
                throw new InvalidDataException();
            }
        }
        else if (!ApplicationProjection.IsValid(new(details.Id, details.ClientId, details.DisplayName, null)) ||
            details.RedirectUris is not { Count: 0 } || details.Profile is not null || details.Credential is not null || tag is not null)
        {
            throw new InvalidDataException();
        }
        return new(StatusCodes.Status200OK, details, null, tag);
    }

    /// <summary>Requires the selected managed profile and exact finite lifecycle/presentation fields, excluding native protected state.</summary>
    private static bool Valid(ApplicationDetails details) => ApplicationEditorRules.IsText(details.Id, ApplicationProjection.IdentifierLength) &&
        ApplicationEditorRules.IsClientId(details.ClientId) && ApplicationEditorRules.Validate(new ApplicationEdit(details.DisplayName!, details.RedirectUris)).Count == 0 &&
        details.Profile == ApplicationEditorRules.Profile && details.Editable && details.ExpiresAt.Offset == TimeSpan.Zero &&
        details.Credential is { } dates && dates.IssuedAt.Offset == TimeSpan.Zero && dates.ExpiresAt.Offset == TimeSpan.Zero &&
        dates.IssuedAt.Ticks % TimeSpan.TicksPerSecond == 0 && dates.ExpiresAt.Ticks % TimeSpan.TicksPerSecond == 0 && dates.ExpiresAt > dates.IssuedAt;

    /// <summary>Accepts only the canonical finite native-generated secret shape, never arbitrary downstream credential text.</summary>
    private static bool ValidSecret(string secret)
    {
        Span<byte> buffer = stackalloc byte[SecretBytes];
        return Convert.TryFromBase64String(secret, buffer, out var written) && written == SecretBytes && Convert.ToBase64String(buffer) == secret;
    }
}
