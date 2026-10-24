using IdentityPlatform.Core.Features.Identity.Authentication;
using IdentityPlatform.Infrastructure.Features.Identity.Ceremonies;
using IdentityPlatform.Infrastructure.Features.Identity.Journeys;

namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Adapts exact browser requests to native journey services and releases responses only after their commits.</summary>
internal sealed class AdministratorJourney(JourneyPassword passwords, JourneyCompletion completion, JourneyStaging staging,
    JourneyDenial denial, DummyPassword dummy, AdministratorTicketPermit tickets) : IAdministratorJourney
{
    /// <summary>Rejects ambiguous credentials, applies bounded dummy work for unavailable accounts and binds admitted continuations.</summary>
    public async Task<string?> BeginAsync(HttpContext context, bool enrollment)
    {
        var request = await AdministratorPasswordForm.ReadAsync(context);
        if (request is null)
        {
            return null;
        }
        var purpose = enrollment ? AdministratorPolicy.Enrollment : AdministratorPolicy.Login;
        var result = await passwords.BeginAsync(request.Username, request.Password,
            purpose, request.Browser, request.Correlation, context.RequestAborted);
        if (result.Interaction is null)
        {
            if (result.NeedsDummyWork)
            {
                dummy.Verify(request.Password);
                await denial.WriteAsync(context.RequestAborted);
            }
            return null;
        }
        context.RequestAborted.ThrowIfCancellationRequested();
        AdministratorBrowser.Bind(context, result.Interaction, request.Browser, request.Correlation);
        var path = enrollment ? AdministratorBrowser.EnrollmentPath : AdministratorBrowser.MfaPath;
        const string Query = "?Interaction=";
        return path + Query + result.Interaction;
    }

    /// <summary>Withholds tickets and response codes until successful verification, audit commit and transaction disposal.</summary>
    public async Task<IReadOnlyList<string>?> CompleteAsync(HttpContext context, bool enrollment)
    {
        var form = await AdministratorBrowser.FormAsync(context, AdministratorBrowser.Interaction, AdministratorBrowser.Code);
        if (form is null || !CeremonySecrets.IsIdentifier(form[AdministratorBrowser.Interaction][0]))
        {
            return null;
        }
        var publicId = form[AdministratorBrowser.Interaction][0]!;
        var code = form[AdministratorBrowser.Code][0]!;
        if (code.Length != AdministratorPolicy.CodeDigits || code.Any(character => character is < '0' or > '9'))
        {
            return null;
        }
        var purpose = enrollment ? AdministratorPolicy.Enrollment : AdministratorPolicy.Login;
        var grant = await completion.CompleteStandaloneAsync(publicId, AdministratorBrowser.Correlation(context, publicId), purpose,
            code, context.RequestAborted);
        if (grant is null)
        {
            return null;
        }
        await tickets.IssueAsync(context, grant);
        return Array.AsReadOnly(grant.RecoveryCodes);
    }

    /// <summary>Returns protected staged material only to the current exact browser-bound enrollment interaction.</summary>
    public Task<string?> StagedAsync(HttpContext context)
    {
        var publicId = AdministratorBrowser.Locator(context);
        return publicId is null ? Task.FromResult<string?>(null) : staging.ReadAsync(publicId,
            AdministratorBrowser.Correlation(context, publicId), context.RequestAborted);
    }
}
