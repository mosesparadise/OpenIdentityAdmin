using System.Globalization;
using System.Text;
using IdentityPlatform.Administrator.Tests.Fixtures;
using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features;

/// <summary>Retains only closed stage/route categories and status codes, never browser URLs, form fields or exception text.</summary>
internal sealed class LocalDemoSignInEvidence
{
    private const string Issuer = "https://issuer.dev.localhost:17443";
    private const string Client = "https://bff.dev.localhost:17444";
    private const string PasswordPath = "/account/administrator/browser/login";
    private const string FactorPath = "/account/administrator/browser/mfa";
    private const string CallbackPath = "/bff/callback";
    private const string ErrorPath = "/bff/login/error";
    private const string LoginPath = "/bff/login";
    private const string ResumePath = "/bff/login/resume";
    private const string SessionPath = "/bff/session";
    private const string Post = "POST";
    private const string ErrorSummary = "#error-summary";
    private const string Summary = " Sign-in stage={0}; category={1}; location={2}; password-status={3}; factor-status={4}; callback-status={5}; error-visible={6}; through-failure-window={7}.";
    private static readonly CompositeFormat SummaryFormat = CompositeFormat.Parse(Summary);
    private int passwordStatus;
    private int factorStatus;
    private int callbackStatus;
    internal Stage Current
    {
        get; set;
    }

    /// <summary>Observes only responses at fixed synthetic origins/routes; URL strings and response bodies are never retained.</summary>
    internal void Observe(object? sender, IResponse response)
    {
        var location = Locate(response.Url);
        if (location == Location.Password && response.Request.Method == Post)
        {
            Interlocked.Exchange(ref passwordStatus, response.Status);
        }
        else if (location == Location.Factor && response.Request.Method == Post)
        {
            Interlocked.Exchange(ref factorStatus, response.Status);
        }
        else if (location == Location.Callback)
        {
            Interlocked.Exchange(ref callbackStatus, response.Status);
        }
    }

    /// <summary>Builds a safe diagnostic after failure; the original exception, page contents, codes and timestamps are not included.</summary>
    internal async Task<LocalDemoSignInFailure> FailureAsync(IPage page, Exception error, string? seed, string? code, long sampled)
    {
        bool? visible = null;
        try
        {
            visible = await page.Locator(ErrorSummary).IsVisibleAsync();
        }
        catch (PlaywrightException) { }
        var category = error switch
        {
            OperationCanceledException => Failure.Canceled,
            TimeoutException => Failure.Timeout,
            PlaywrightException => Failure.Browser,
            Xunit.Sdk.XunitException => Failure.Assertion,
            _ => Failure.Unexpected
        };
        var summary = string.Format(CultureInfo.InvariantCulture, SummaryFormat, Current, category, Locate(page.Url),
            Volatile.Read(ref passwordStatus), Volatile.Read(ref factorStatus), Volatile.Read(ref callbackStatus), visible, Window(seed, code, sampled));
        TestContext.Current.TestOutputHelper?.WriteLine(summary);
        return new(summary);
    }

    /// <summary>Reports validity through failure, including any callback wait; this cannot establish validity at issuer verification.</summary>
    private static WindowState Window(string? seed, string? code, long sampled)
    {
        if (seed is null || code is null)
        {
            return WindowState.NotSelected;
        }
        try
        {
            FactorOracle.RequireWindow(seed, code, sampled, valid: true);
            return WindowState.Valid;
        }
        catch (Xunit.Sdk.XunitException) { return WindowState.OutsideObservedBound; }
    }

    /// <summary>Classifies only known exact origin/path pairs, discarding query strings and every unknown destination.</summary>
    private static Location Locate(string raw)
    {
        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri))
        {
            return Location.Other;
        }
        var authority = uri.GetLeftPart(UriPartial.Authority);
        return (authority, uri.AbsolutePath) switch
        {
            (Issuer, PasswordPath) => Location.Password,
            (Issuer, FactorPath) => Location.Factor,
            (Client, CallbackPath) => Location.Callback,
            (Client, ErrorPath) => Location.ClientError,
            (Client, LoginPath) => Location.ClientLogin,
            (Client, ResumePath) => Location.ClientResume,
            (Client, SessionPath) => Location.ClientStatus,
            _ => Location.Other
        };
    }

    internal enum Stage
    {
        Navigation, Start, Password, FactorForm, CodeSelection, FactorSubmission, Callback, Status
    }
    private enum Location
    {
        Other, Password, Factor, Callback, ClientError, ClientLogin, ClientResume, ClientStatus
    }
    private enum Failure
    {
        Canceled, Timeout, Browser, Assertion, Unexpected
    }
    private enum WindowState
    {
        NotSelected, Valid, OutsideObservedBound
    }
}

/// <summary>Carries only the observer's closed diagnostic fields across owned browser disposal, without an unsafe inner exception.</summary>
internal sealed class LocalDemoSignInFailure(string summary) : Exception(summary);
