namespace IdentityPlatform.Admin.Bff.Features.BrowserSessions;

/// <summary>Marks an explicitly composed workspace and supplies fixed presentation-only destinations.</summary>
internal sealed class BrowserWorkspaceNavigation
{
    private const string Workspace = "/";
    private const string ReturnLink = "<p><a id=\"workspace-return\" href=\"/\">Back to workspace</a></p>";

    /// <summary>Retains the ordinary login landing unless the local workspace was explicitly registered.</summary>
    internal static string Destination(HttpContext http) => http.RequestServices?.GetService<BrowserWorkspaceNavigation>() is null
        ? BrowserLoginBoundary.Login : Workspace;

    /// <summary>Adds only a fixed local navigation link; no query, Host or claimed session state influences the markup.</summary>
    internal static string Link(HttpContext http) => http.RequestServices?.GetService<BrowserWorkspaceNavigation>() is null ? string.Empty : ReturnLink;
}
