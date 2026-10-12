using IdentityPlatform.AuthServer.Features.Accounts.Shared;

namespace IdentityPlatform.AuthServer.Features.Protocol.Protection;

/// <summary>Preserves the pilot middleware and logger identity while delegating to shared account protection.</summary>
/// <example><code>app.UseMiddleware&lt;PilotRequestProtection&gt;();</code></example>
public sealed class PilotRequestProtection(RequestDelegate next, ILogger<PilotRequestProtection> logger)
{
    /// <summary>Runs the common request guard, preserving the pilot's safe event category and identifier.</summary>
    /// <example><code>app.UseMiddleware&lt;PilotRequestProtection&gt;();</code></example>
    public Task InvokeAsync(HttpContext context) => AccountRequestGuard.InvokeAsync(context, next, incident => RequestFailed(logger, incident));

    private const int FailureEvent = 1001;
    private const string FailureTemplate = "Pilot request failed. Incident {IncidentId}.";
    private static readonly Action<ILogger, string, Exception?> Failure = LoggerMessage.Define<string>(
        LogLevel.Warning, new EventId(FailureEvent), FailureTemplate);

    /// <summary>Records only a generated incident without attaching an exception or request data.</summary>
    private static void RequestFailed(ILogger logger, string incidentId) => Failure(logger, incidentId, null);
}
