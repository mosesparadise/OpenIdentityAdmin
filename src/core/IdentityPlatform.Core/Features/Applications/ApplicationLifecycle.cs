using System.ComponentModel.DataAnnotations;

namespace IdentityPlatform.Core.Features.Applications;

/// <summary>Requests one explicitly confirmed lifecycle transition without accepting credential, grant or arbitrary audit text.</summary>
/// <example><code>var input = new ApplicationLifecycle("maintenance", true); // Submit with the current strong application ETag.</code></example>
public sealed record ApplicationLifecycle([property: Required, MaxLength(ApplicationLifecycleRules.MaximumReasonLength)] string Reason, bool Confirm);

/// <summary>Names the closed persisted reason codes independently of provider text comparison rules.</summary>
/// <example><code>var reason = ApplicationLifecycleReason.Maintenance;</code></example>
public enum ApplicationLifecycleReason
{
    Maintenance = 1,
    Security = 2,
    Configuration = 3
}

/// <summary>Validates the exact public reason vocabulary and confirmation for both lifecycle actions.</summary>
/// <example><code>if (ApplicationLifecycleRules.Reason(input) is null) return Results.BadRequest();</code></example>
public static class ApplicationLifecycleRules
{
    private const string Maintenance = "maintenance";
    private const string Security = "security";
    private const string Configuration = "configuration";
    public const int MaximumReasonLength = 16;
    public const string DisableSuffix = "/disable";
    public const string EnableSuffix = "/enable";

    /// <summary>Returns a finite reason only for confirmed exact lowercase input; whitespace, case aliases and unknown values fail without echoing input.</summary>
    /// <example><code>var reason = ApplicationLifecycleRules.Reason(new("security", true));</code></example>
    public static ApplicationLifecycleReason? Reason(ApplicationLifecycle input) => !input.Confirm ? null : input.Reason switch
    {
        Maintenance => ApplicationLifecycleReason.Maintenance,
        Security => ApplicationLifecycleReason.Security,
        Configuration => ApplicationLifecycleReason.Configuration,
        _ => null
    };
}
