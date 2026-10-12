namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Suppresses framework categories that may serialize account or protocol secrets, including reload overrides.</summary>
internal static class AccountDiagnostics
{
    private static readonly string[] Categories = ["OpenIddict", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore"];

    /// <summary>Applies provider-aware suppression on every options reload while retaining safe product diagnostics.</summary>
    internal static void SuppressSecrets(IServiceCollection services)
    {
        services.PostConfigure<LoggerFilterOptions>(options =>
        {
            for (var index = 0; index < options.Rules.Count; index++)
            {
                var rule = options.Rules[index];
                options.Rules[index] = new LoggerFilterRule(rule.ProviderName, rule.CategoryName, rule.LogLevel,
                    (provider, category, level) => !Sensitive(category) && (rule.Filter?.Invoke(provider, category, level) ?? true));
            }
            foreach (var category in Categories)
            {
                options.Rules.Add(new LoggerFilterRule(null, category, LogLevel.None, null));
            }
        });
    }

    /// <summary>Recognizes the fixed framework namespaces whose diagnostics can contain credentials.</summary>
    internal static bool Sensitive(string? category) => category is not null &&
        Categories.Any(prefix => category.StartsWith(prefix, StringComparison.Ordinal));
}
