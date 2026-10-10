using System.Diagnostics;
using OpenTelemetry;

namespace IdentityPlatform.ServiceDefaults.Features.Telemetry;

public sealed class OperationalTraceProcessor : BaseProcessor<Activity>
{
    private static readonly HashSet<string> AllowedTags = new(StringComparer.Ordinal)
    {
        "http.request.method", "http.response.status_code", "http.route",
        "network.protocol.version", "url.scheme", "server.port"
    };

    public override void OnEnd(Activity activity)
    {
        activity.TraceStateString = null;
        // Activity events/links are immutable. Suppress those spans instead of exporting
        // potentially sensitive exception data or linked attributes. Exporter tests cover this.
        if (activity.Events.Any() || activity.Links.Any() || activity.Baggage.Any())
        {
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            return;
        }
        foreach (var tag in activity.TagObjects.ToArray())
            if (!AllowedTags.Contains(tag.Key)) activity.SetTag(tag.Key, null);
        activity.SetStatus(activity.Status);
        activity.DisplayName = activity.GetTagItem("http.route") is "/alive" or "/health"
            ? $"GET {activity.GetTagItem("http.route")}" : "operational health";
    }
}
