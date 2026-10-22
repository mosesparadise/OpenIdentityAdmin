using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Host.Tests.Features.AdministratorClient;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Produces real signed fixture tokens and runs the installed native cryptographic handlers without browser or database substitutes.</summary>
internal sealed class BrowserSessionToken(string encoded, ClaimsPrincipal principal, DateTimeOffset now)
{
    internal const string SubjectClaim = "sub";
    internal const string SessionClaim = "sid";
    internal const string AuthenticationTimeClaim = "auth_time";
    internal const string SourceExpiryClaim = "admin_session_expires_at";
    internal const string IntentClaim = "admin_login_intent";
    internal const string MethodsClaim = "amr";
    internal const string IssuedAtClaim = "iat";
    internal const string ExpiresAtClaim = "exp";
    internal const string IssuerClaim = "iss";
    internal const string AudienceClaim = "aud";
    internal const string RoleClaim = "role";
    internal const string PermissionClaim = "permission";
    internal const string Intent = "ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789ABCDEF0123456789";
    private const int PasswordAgeSeconds = 10;
    private const int SourceLifetimeHours = 8;
    private const string SyntheticRole = "synthetic-untrusted-role";
    private const string SyntheticPermission = "synthetic-permission";
    private static readonly string[] Methods = ["pwd", "otp"];
    internal const string Subject = "synthetic-b3-subject";
    internal const string Session = "synthetic-b3-session";
    internal string Encoded { get; } = encoded;
    internal ClaimsPrincipal Principal { get; } = principal;
    internal DateTimeOffset Now { get; } = now;

    /// <summary>Signs original JSON, preserving hostile duplicate and scalar/array forms that a dictionary-based fixture would erase.</summary>
    internal static async Task<BrowserSessionToken> CreateAsync(NativeClientCredentialFixture fixture,
        Func<string, string>? change = null, bool nativeRejectionExpected = false)
    {
        using var credentials = NativeClientProfileTests.Load(fixture);
        await using var services = NativeClientCompositionTests.Create(credentials);
        var options = services.GetRequiredService<IOptionsMonitor<OpenIddictClientOptions>>().CurrentValue;
        var registration = Assert.Single(options.Registrations);
        var signing = Assert.Single(registration.SigningCredentials);
        var now = DateTimeOffset.FromUnixTimeSeconds(DateTimeOffset.UtcNow.ToUnixTimeSeconds());
        var payload = Payload(now, registration.ClientId!);
        var token = options.JsonWebTokenHandler.CreateToken(change?.Invoke(payload) ?? payload, signing);
        var configuration = new OpenIddictConfiguration { Issuer = registration.Issuer };
        configuration.SigningKeys.Add(signing.Key);
        var context = new OpenIddictClientEvents.ValidateTokenContext(new OpenIddictClientTransaction
        {
            Options = options,
            Registration = registration,
            Configuration = configuration,
            Logger = NullLogger.Instance
        })
        {
            Token = token
        };
        context.ValidTokenTypes.Add(OpenIddictConstants.TokenTypeIdentifiers.IdentityToken);
        await new OpenIddictClientHandlers.Protection.ResolveTokenValidationParameters().HandleAsync(context);
        await new NativeClientValidationParameters(services.GetRequiredService<NativeClientProfile>()).HandleAsync(context);
        await new OpenIddictClientHandlers.Protection.ValidateIdentityModelToken().HandleAsync(context);
        Assert.Equal(nativeRejectionExpected, context.IsRejected);
        if (nativeRejectionExpected)
        {
            Assert.Equal(OpenIddictConstants.Errors.InvalidToken, context.Error);
            return new(token, new ClaimsPrincipal(), now);
        }
        await new OpenIddictClientHandlers.Protection.MapInternalClaims().HandleAsync(context);
        return new(token, Assert.IsType<ClaimsPrincipal>(context.Principal), now);
    }

    /// <summary>Uses a source deadline later than ID expiry and based on completion, deliberately distinct from password authentication time.</summary>
    private static string Payload(DateTimeOffset now, string client) => JsonSerializer.Serialize(new Dictionary<string, object>
    {
        [IssuerClaim] = NativeClientProfileTests.Issuer,
        [AudienceClaim] = client,
        [IssuedAtClaim] = now.ToUnixTimeSeconds(),
        [ExpiresAtClaim] = now.AddMinutes(1).ToUnixTimeSeconds(),
        [SubjectClaim] = Subject,
        [SessionClaim] = Session,
        [AuthenticationTimeClaim] = now.AddSeconds(-PasswordAgeSeconds).ToUnixTimeSeconds(),
        [MethodsClaim] = Methods,
        [SourceExpiryClaim] = now.AddHours(SourceLifetimeHours).ToUnixTimeSeconds(),
        [IntentClaim] = Intent,
        [RoleClaim] = SyntheticRole,
        [PermissionClaim] = SyntheticPermission
    });

    /// <summary>Replaces one field in already encoded JSON with an explicit raw shape while preserving all other cryptographic inputs.</summary>
    internal static string Field(string payload, string name, string? replacement)
    {
        using var document = JsonDocument.Parse(payload);
        var properties = document.RootElement.EnumerateObject().Select(property => property.Name == name ?
            replacement is null ? null : JsonSerializer.Serialize(name) + ':' + replacement :
            JsonSerializer.Serialize(property.Name) + ':' + property.Value.GetRawText());
        return '{' + string.Join(',', properties.Where(value => value is not null)) + '}';
    }

    /// <summary>Formats test-controlled epoch values without culture-dependent dates.</summary>
    internal static string Epoch(DateTimeOffset value) => value.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
}
