namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Connects shared request protection to neutral administrator diagnostics.</summary>
internal sealed class AccountRequestProtection(RequestDelegate next, ILogger<AccountRequestProtection> logger)
{
    /// <summary>Runs the shared guard and logs only a generated incident reference on failure.</summary>
    public Task InvokeAsync(HttpContext context) => AccountRequestGuard.InvokeAsync(context, next, incident => RequestFailed(logger, incident));

    private const int FailureEvent = 1002;
    private const string FailureTemplate = "Account request failed. Incident {IncidentId}.";
    private static readonly Action<ILogger, string, Exception?> Failure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(FailureEvent), FailureTemplate);

    /// <summary>Records only a generated incident without attaching an exception or request data.</summary>
    private static void RequestFailed(ILogger logger, string incidentId) => Failure(logger, incidentId, null);
}
