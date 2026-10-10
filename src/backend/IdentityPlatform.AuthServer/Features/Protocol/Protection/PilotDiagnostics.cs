namespace IdentityPlatform.AuthServer.Features.Protocol.Protection;

public static class PilotDiagnostics
{
    private static readonly string[] Categories = ["OpenIddict", "Microsoft.AspNetCore", "Microsoft.EntityFrameworkCore"];

    public static void SuppressProtocolSecrets(IServiceCollection services)
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

    public static bool Sensitive(string? category) => category is not null &&
        (category.StartsWith("OpenIddict", StringComparison.Ordinal) ||
         category.StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal) ||
         category.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
}
