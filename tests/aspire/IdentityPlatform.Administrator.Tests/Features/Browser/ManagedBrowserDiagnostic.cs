using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Records finite relying-party progress without retaining protocol values, bodies, exception messages or private browser state.</summary>
internal sealed class ManagedBrowserDiagnostic
{
    private const int FirstHttpStatus = 100;
    private const int LastHttpStatus = 599;
    private const string StartPath = "/start";
    private const string CallbackPath = "/callback";
    private const string CompletePath = "/complete";
    private const string AccountPath = "/account/application/sign-in";
    private const string AuthorizePath = "/connect/authorize";
    private static readonly CompositeFormat Summary = CompositeFormat.Parse(
        "managed-browser repeated={0} stage={1} failure={2} token-status={3} callbacks={4} exchanges={5} completion={6} location={7}");
    private int stage;
    private int failure;
    private int tokenStatus;
    internal bool Repeated
    {
        get; init;
    }
    internal Stage Current
    {
        /// <summary>Reads the last finite progress observation across the browser and relying-party threads.</summary>
        get => (Stage)Volatile.Read(ref stage);
        /// <summary>Publishes only a declared progress category.</summary>
        set => Volatile.Write(ref stage, Enum.IsDefined(value) ? (int)value : (int)Stage.Unknown);
    }

    /// <summary>Retains only a standard HTTP status after the real exchange returns.</summary>
    internal void Response(int status) => Volatile.Write(ref tokenStatus, status is >= FirstHttpStatus and <= LastHttpStatus ? status : 0);

    /// <summary>Classifies failure without inspecting or preserving its message, inner exceptions or provider values.</summary>
    internal void Failed(Exception error) => Volatile.Write(ref failure, (int)(error switch
    {
        OperationCanceledException => Failure.Canceled,
        TimeoutException => Failure.Timeout,
        PlaywrightException => Failure.Browser,
        Xunit.Sdk.XunitException => Failure.Assertion,
        _ => Failure.Unexpected
    }));

    /// <summary>Projects transient location into exact route categories and bounds all counters; no URL or exception string reaches output.</summary>
    internal string Describe(string? location, Uri issuer, Uri relyingParty, int callbacks, int exchanges, bool? completion) =>
        string.Format(CultureInfo.InvariantCulture, Summary, Repeated, Current, (Failure)Volatile.Read(ref failure),
            Volatile.Read(ref tokenStatus), Count(callbacks), Count(exchanges),
            completion is true ? Completion.Success : completion is false ? Completion.Failure : Completion.Pending,
            Locate(location, issuer, relyingParty));

    /// <summary>Distinguishes an expected single call from absent or repeated work without retaining arbitrary integers.</summary>
    private static CountState Count(int count) => count == 0 ? CountState.None : count == 1 ? CountState.Once : CountState.Other;

    /// <summary>Discards queries and fragments; only exact known authorities and paths produce named locations.</summary>
    private static Location Locate(string? raw, Uri issuer, Uri relyingParty)
    {
        if (raw is null)
        {
            return Location.Closed;
        }
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return Location.Other;
        }
        var authority = uri.GetLeftPart(UriPartial.Authority);
        if (authority == relyingParty.GetLeftPart(UriPartial.Authority))
        {
            return uri.AbsolutePath switch
            {
                StartPath => Location.RpStart,
                CallbackPath => Location.RpCallback,
                CompletePath => Location.RpComplete,
                _ => Location.Other
            };
        }
        return authority == issuer.GetLeftPart(UriPartial.Authority) ? uri.AbsolutePath switch
        {
            AccountPath => Location.IssuerAccount,
            AuthorizePath => Location.IssuerAuthorize,
            _ => Location.Other
        } : Location.Other;
    }

    internal enum Stage
    {
        Prepared, Started, CallbackReceived, CorrelationRejected, CorrelationAccepted, ExchangeStarted,
        TokenResponseReceived, TokenShapeAccepted, IdentityValidated, Completed, Unknown
    }
    private enum Failure
    {
        None, Canceled, Timeout, Browser, Assertion, Unexpected
    }
    private enum Location
    {
        Other, Closed, RpStart, RpCallback, RpComplete, IssuerAccount, IssuerAuthorize
    }
    private enum CountState
    {
        None, Once, Other
    }
    private enum Completion
    {
        Pending, Success, Failure
    }
}
