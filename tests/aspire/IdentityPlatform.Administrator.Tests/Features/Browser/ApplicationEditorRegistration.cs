namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Retains the exact UI-created and edited client's current credential privately for its subsequent real sign-in.</summary>
internal sealed class ApplicationEditorRegistration(string id, string clientId, string callback, string secret, string previous)
{
    internal string Id => id;
    internal string ClientId => clientId;
    internal string Callback => callback;
    internal string Secret => secret;
    internal string[] Secrets => [previous, secret];
}
