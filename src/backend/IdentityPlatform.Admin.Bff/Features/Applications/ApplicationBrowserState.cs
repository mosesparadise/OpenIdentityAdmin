using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Admin.Bff.Features.Sessions;

namespace IdentityPlatform.Admin.Bff.Features.Applications;

/// <summary>Retains one authenticated generation and protected credential without keeping a Sessions scope open across HTTP.</summary>
internal sealed class ApplicationBrowserState(BrowserSessionRead session, BrowserAccessCredential access, BrowserRequestBinding binding)
{
    internal BrowserAccessCredential Access => access;
    internal BrowserRequestBinding Binding => binding;

    /// <summary>Disposes the actual read scope before returning; generation and credential come from one joined durable observation.</summary>
    internal static async Task<ApplicationBrowserState?> ReadAsync(HttpContext http, TimeProvider clock)
    {
        await using var scope = http.RequestServices.GetRequiredService<IServiceScopeFactory>().CreateAsyncScope();
        var session = await scope.ServiceProvider.GetRequiredService<BrowserSessionReader>().ReadAsync(http);
        var access = session?.Access(scope.ServiceProvider.GetRequiredService<SessionTicketCodec>(), clock.GetUtcNow());
        var binding = session?.ApplicationBinding(clock.GetUtcNow());
        return session is not null && access is not null && binding is not null ? new(session, access, binding) : null;
    }

    /// <summary>Rechecks original deadlines without renewing authority or consulting disposed services.</summary>
    internal DateTimeOffset? Expiry(DateTimeOffset now) => session.Status(now) is { Authenticated: true, ExpiresAt: { } expiry } && access.ExpiresAt > now
        ? (expiry < access.ExpiresAt ? expiry : access.ExpiresAt) : null;

    /// <summary>Suppresses publication on a changed generation or credential; this cannot roll back an already admitted remote mutation.</summary>
    internal bool Same(ApplicationBrowserState current) => binding.AdditionalData == current.Binding.AdditionalData && access.Value == current.Access.Value;
}
