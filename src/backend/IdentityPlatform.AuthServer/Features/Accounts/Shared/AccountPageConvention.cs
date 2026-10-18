using IdentityPlatform.AuthServer.Features.Protocol.Hosting;
using Microsoft.AspNetCore.Mvc.ApplicationModels;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Fails closed for disabled or unknown compiled pages instead of exposing every Razor page through MapRazorPages.</summary>
internal sealed class AccountPageConvention(bool pilot, bool administrator) : IPageRouteModelConvention
{
    private static readonly Dictionary<string, string> PilotPages = new(StringComparer.Ordinal)
    {
        ["/Login/Index"] = "account/login",
        ["/Login/Complete"] = "account/complete"
    };
    private static readonly Dictionary<string, string> AdministratorPages = new(StringComparer.Ordinal)
    {
        ["/Enrollment/Index"] = "account/administrator/enrollment",
        ["/Mfa/Login"] = "account/administrator/login",
        ["/Mfa/Index"] = "account/administrator/mfa",
        ["/Recovery/Index"] = "account/administrator/recovery",
        ["/Recovery/Verify"] = "account/administrator/recovery/verify",
        ["/Security/Replacement"] = "account/administrator/security/replacement",
        ["/Security/Replace"] = "account/administrator/security/replacement/verify",
        ["/Security/Confirm"] = "account/administrator/security/replacement/confirm",
        ["/Security/Regeneration"] = "account/administrator/security/recovery-codes",
        ["/Security/Regenerate"] = "account/administrator/security/recovery-codes/verify",
        ["/Security/Index"] = "account/administrator/security",
        ["/Security/Logout"] = "account/administrator/logout"
    };

    /// <summary>Retains only an enabled page's exact intended route; aliases and unknown pages receive no selectors.</summary>
    public void Apply(PageRouteModel model)
    {
        var route = AllowedRoute(model.ViewEnginePath);
        for (var index = model.Selectors.Count - 1; index >= 0; index--)
        {
            var template = model.Selectors[index].AttributeRouteModel?.Template?.TrimStart('/');
            if (route is null || !string.Equals(template, route, StringComparison.Ordinal))
            {
                model.Selectors.RemoveAt(index);
            }
            else if (pilot && PilotPages.ContainsKey(model.ViewEnginePath))
            {
                model.Selectors[index].EndpointMetadata.Add(new PilotAccountEndpoint());
            }
        }
    }

    /// <summary>Resolves an explicitly supported page only when its feature was enabled at composition.</summary>
    private string? AllowedRoute(string page)
    {
        if (pilot && PilotPages.TryGetValue(page, out var pilotRoute))
        {
            return pilotRoute;
        }
        return administrator && AdministratorPages.TryGetValue(page, out var adminRoute) ? adminRoute : null;
    }
}
