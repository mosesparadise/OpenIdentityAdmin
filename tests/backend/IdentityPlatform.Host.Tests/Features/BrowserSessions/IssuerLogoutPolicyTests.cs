using IdentityPlatform.AuthServer.Features.Accounts.Browser;
using IdentityPlatform.AuthServer.Features.Protocol.Administrator;
using IdentityPlatform.Infrastructure.Features.Identity.Factors;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Primitives;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Host.Tests.Features.BrowserSessions;

/// <summary>Checks strict logout framing and purpose-separated original confirmation bounds without simulating authenticated HTTP acceptance.</summary>
public sealed class IssuerLogoutPolicyTests
{
    private const string Unexpected = "unexpected";
    private const string AntiforgeryField = "__RequestVerificationToken";
    private const string AntiforgeryPlaceholder = "native-antiforgery-placeholder";
    private const string ConfirmationField = "confirmation";
    private const string ConfirmationPlaceholder = "protected-confirmation-placeholder";
    private const string Subject = "subject";
    private const string OtherSubject = "other";
    private const string Foreign = "https://foreign.example.test/";
    private const string ForeignPurpose = "foreign-purpose";
    private const string PrivatePayload = "private";
    private const string ClientField = "client_id";
    private const string ClientValue = "identity-admin-bff";
    private const string ReturnField = "post_logout_redirect_uri";
    private const string ReturnValue = "https://administrator.example.test/bff/logout/return";
    private const string HintField = "logout_hint";
    private const string StateField = "state";

    private const int SourceLifetime = 100;
    private const int IdleSeconds = 1800;
    private const int AbsoluteSeconds = 28800;
    private const long Now = 1800000000;
    private const string Selector = "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA";
    private const string Other = "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBA";

    /// <summary>Every missing, extra, duplicate or aliased native field denies while the exact original shape remains accepted.</summary>
    [Theory]
    [InlineData(ClientField)]
    [InlineData(ReturnField)]
    [InlineData(HintField)]
    [InlineData(StateField)]
    public void Given_ExactNativeFields_When_OneFieldIsMissingDuplicatedOrAliased_Then_FramingDenies(string field)
    {
        var original = Fields();
        Assert.True(IssuerLogoutInput.Allows(original, false));
        var absent = new Dictionary<string, StringValues>(original);
        absent.Remove(field);
        Assert.False(IssuerLogoutInput.Allows(absent, false));
        var duplicate = new Dictionary<string, StringValues>(original) { [field] = new StringValues([original[field].ToString(), original[field].ToString()]) };
        Assert.False(IssuerLogoutInput.Allows(duplicate, false));
        absent[field.ToUpperInvariant()] = original[field];
        Assert.False(IssuerLogoutInput.Allows(absent, false));
    }

    /// <summary>Credentials, native override fields and unknown values never join the four-field browser contract.</summary>
    [Theory]
    [InlineData("id_token_hint")]
    [InlineData("access_token")]
    [InlineData("client_secret")]
    [InlineData("request_uri")]
    [InlineData("redirect_uri")]
    public void Given_ExactNativeFields_When_UnsupportedValueIsAdded_Then_FramingDenies(string field)
    {
        var fields = Fields();
        fields[field] = Unexpected;
        Assert.False(IssuerLogoutInput.Allows(fields, false));
    }

    /// <summary>Native antiforgery alone cannot authorize logout; both explicit confirmation values are required only on POST.</summary>
    [Fact]
    public void Given_PostFraming_When_ProtectedConfirmationOrAntiforgeryIsMissing_Then_NoInputIsAccepted()
    {
        var fields = Fields();
        Assert.False(IssuerLogoutInput.Allows(fields, true));
        fields[AntiforgeryField] = AntiforgeryPlaceholder;
        Assert.False(IssuerLogoutInput.Allows(fields, true));
        fields[ConfirmationField] = ConfirmationPlaceholder;
        Assert.True(IssuerLogoutInput.Allows(fields, true));
        Assert.False(IssuerLogoutInput.Allows(fields, false));
    }

    /// <summary>Subject/session, all native values, key purpose and original time are independently bound by real Data Protection.</summary>
    [Fact]
    public void Given_ProtectedConfirmation_When_IdentityRequestKeyOrClockChanges_Then_OriginalBindingCannotBeAdopted()
    {
        var clock = new Clock();
        var protection = new EphemeralDataProtectionProvider();
        var policy = new IssuerLogoutConfirmation(protection, clock);
        var request = Request();
        var session = new JourneySession(Selector, Subject, Now, Now, Now + SourceLifetime);
        var proof = policy.Create(request, session);
        var candidate = new JourneySessionCandidate(Subject, Selector);
        Assert.Equal(new JourneyLogoutDeadline(Now, Now + SourceLifetime), policy.Read(proof, request, candidate));
        Assert.Null(policy.Read(proof, request, candidate with
        {
            UserId = OtherSubject
        }));
        Assert.Null(policy.Read(proof, request, candidate with
        {
            SessionId = Other
        }));
        request.State = Other;
        Assert.Null(policy.Read(proof, request, candidate));
        request = Request();
        request.PostLogoutRedirectUri = Foreign;
        Assert.Null(policy.Read(proof, request, candidate));
        Assert.Null(new IssuerLogoutConfirmation(new EphemeralDataProtectionProvider(), clock).Read(proof, Request(), candidate));
        Assert.Null(policy.Read(protection.CreateProtector(ForeignPurpose).Protect(PrivatePayload), Request(), candidate));
        clock.Seconds = Now + SourceLifetime;
        Assert.Null(policy.Read(proof, Request(), candidate));
        clock.Seconds = Now - 1;
        Assert.Null(policy.Read(proof, Request(), candidate));
    }

    /// <summary>Pure retained source bounds reject rollback and exact idle or absolute expiry after the live context has been released.</summary>
    [Fact]
    public void Given_OriginalSourceDeadline_When_ClockReachesEitherBoundaryOrRollsBack_Then_PublicationAuthorityExpires()
    {
        var source = new JourneySessionDeadline(Now, Now + AbsoluteSeconds, Now);
        source.RequireCurrent(Now);
        source.RequireCurrent(Now + IdleSeconds - 1);
        Assert.Throws<FactorUnavailableException>(() => source.RequireCurrent(Now - 1));
        Assert.Throws<FactorUnavailableException>(() => source.RequireCurrent(Now + IdleSeconds));
        source = new JourneySessionDeadline(Now, Now + SourceLifetime, Now);
        source.RequireCurrent(Now + SourceLifetime - 1);
        Assert.Throws<FactorUnavailableException>(() => source.RequireCurrent(Now + SourceLifetime));
    }

    /// <summary>Creates independent expected browser transport fields, without using production field-name constants as the oracle.</summary>
    private static Dictionary<string, StringValues> Fields() => new()
    {
        [ClientField] = ClientValue,
        [ReturnField] = ReturnValue,
        [HintField] = Selector,
        [StateField] = Selector
    };

    /// <summary>Supplies a pure native request for cryptographic policy checks, never an authentication result.</summary>
    private static OpenIddictRequest Request()
    {
        var request = new OpenIddictRequest { ClientId = ClientValue, PostLogoutRedirectUri = ReturnValue, State = Selector };
        request.SetParameter(HintField, Selector);
        return request;
    }

    /// <summary>Moves only the policy clock; no sleeping or timeout changes are needed for equality controls.</summary>
    private sealed class Clock : TimeProvider
    {
        internal long Seconds { get; set; } = Now;
        /// <summary>Returns the exact requested instant.</summary>
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.FromUnixTimeSeconds(Seconds);
    }
}
