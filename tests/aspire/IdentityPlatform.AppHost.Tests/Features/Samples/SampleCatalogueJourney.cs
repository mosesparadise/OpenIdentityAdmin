using IdentityPlatform.Administrator.Tests.Features.Browser;
using Microsoft.Playwright;

namespace IdentityPlatform.AppHost.Tests.Features.Samples;

/// <summary>Creates the sample's complete integration policy through the shipped administrator catalogue and application forms.</summary>
internal static class SampleCatalogueJourney
{
    private const string ChoiceSeparator = " · ";
    private const string Administrator = "https://bff.dev.localhost:17444/";
    private const string Issuer = "https://issuer.dev.localhost:17443/";
    private const string Callback = "https://sample.dev.localhost:17446/callback";
    private const string Audiences = "audiences";
    private const string Scopes = "scopes";
    private const string Applications = "applications";
    private const string CreateAudience = "Create audience";
    private const string CreateScope = "Create scope";
    private const string AudienceDetails = "Audience details";
    private const string ScopeDetails = "Scope details";
    private const string Name = "#catalogue-name";
    private const string Display = "#catalogue-displayName";
    private const string Owner = "#catalogue-owner";
    private const string Consent = "#catalogue-consentText";
    private const string Kind = "#catalogue-kind";
    private const string Disclosure = "#catalogue-disclosure";
    private const string Audience = "#catalogue-audienceId";
    private const string Resource = "orders-api";
    private const string ApiScope = "orders.read";
    private const string EmailScope = "sample.email";
    private const string OwnerName = "Local sample operator";
    private const string ApiConsent = "Read synthetic orders";
    private const string EmailConsent = "Receive your current email and verification status";
    private const string ApiKind = "api";
    private const string IdentityKind = "identity";
    private const string EmailDisclosure = "email";
    private const string DetailsPrefix = "View details for ";
    private const string ApiChoice = "orders.read — orders.read";
    private const string EmailChoice = "sample.email — sample.email";
    private const string ConfirmAssignment = "#assignment-confirm";
    private const string SaveAssignment = "Save scope assignment";
    private const string Assigned = "Saved assignment: additional scopes require explicit user consent";

    /// <summary>Changes current issuer policy through the shipped administrator UI after the sample has received its token.</summary>
    internal static async Task RemoveEmailAsync(IPage page, string clientId)
    {
        await page.GotoAsync(Administrator + Applications);
        await Button(page, DetailsPrefix + clientId).ClickAsync();
        await page.GetByLabel(EmailChoice, new()
        {
            Exact = true
        }).UncheckAsync();
        await page.Locator(ConfirmAssignment).CheckAsync();
        await Button(page, SaveAssignment).ClickAsync();
        await page.GetByText(Assigned, new()
        {
            Exact = false
        }).WaitForAsync();
        await Assertions.Expect(page.GetByLabel(EmailChoice, new()
        {
            Exact = true
        })).Not.ToBeCheckedAsync();
    }

    /// <summary>Uses only normal UI writes and captures only the one-time credential needed by the protected dashboard setup.</summary>
    internal static async Task<ApplicationEditorRegistration> CreateAsync(IPage page)
    {
        await page.GotoAsync(Administrator + Audiences);
        await Button(page, CreateAudience).ClickAsync();
        await page.Locator(Name).FillAsync(Resource);
        await page.Locator(Display).FillAsync(Resource);
        await page.Locator(Owner).FillAsync(OwnerName);
        await Button(page, CreateAudience).ClickAsync();
        await Heading(page, AudienceDetails).WaitForAsync();
        await ScopeAsync(page, ApiScope, ApiKind, ApiConsent);
        await ScopeAsync(page, EmailScope, IdentityKind, EmailConsent);
        var registration = await ApplicationEditorJourney.RunAsync(page, new Uri(Administrator), new Uri(Issuer), DateTimeOffset.UtcNow, callback: Callback);
        await page.GotoAsync(Administrator + Applications);
        await Button(page, DetailsPrefix + registration.ClientId).ClickAsync();
        await page.GetByLabel(ApiChoice, new()
        {
            Exact = true
        }).CheckAsync();
        await page.GetByLabel(EmailChoice, new()
        {
            Exact = true
        }).CheckAsync();
        await page.Locator(ConfirmAssignment).CheckAsync();
        await Button(page, SaveAssignment).ClickAsync();
        await page.GetByText(Assigned, new()
        {
            Exact = false
        }).WaitForAsync();
        return registration;
    }

    /// <summary>Creates one explicit API audience binding or finite email-disclosure scope using visible native select controls.</summary>
    private static async Task ScopeAsync(IPage page, string name, string kind, string consent)
    {
        await page.GotoAsync(Administrator + Scopes);
        await Button(page, CreateScope).ClickAsync();
        await page.Locator(Name).FillAsync(name);
        await page.Locator(Display).FillAsync(name);
        await page.Locator(Consent).FillAsync(consent);
        await page.Locator(Kind).SelectOptionAsync(kind);
        if (kind == ApiKind)
        {
            await page.Locator(Audience).SelectOptionAsync(new SelectOptionValue { Label = Resource + ChoiceSeparator + Resource });
        }
        else
        {
            await page.Locator(Disclosure).SelectOptionAsync(EmailDisclosure);
        }
        await Button(page, CreateScope).ClickAsync();
        await Heading(page, ScopeDetails).WaitForAsync();
    }

    /// <summary>Finds a single named production button rather than scripting internal handlers or hidden API calls.</summary>
    private static ILocator Button(IPage page, string name) => page.GetByRole(AriaRole.Button, new() { Name = name, Exact = true });

    /// <summary>Waits for semantic page completion before starting the next independent UI mutation.</summary>
    private static ILocator Heading(IPage page, string name) => page.GetByRole(AriaRole.Heading, new() { Name = name, Exact = true });
}
