using System.Text.Json;
using IdentityPlatform.CodeQuality.Features.Verification;

namespace IdentityPlatform.CodeQuality.Features.Evidence;

/// <summary>Run deterministic analysis or its seeded controls without modifying source or acceptance inventories.</summary>
internal static class QualityCommand
{
    private const string InventoryCommand = "--inventory";
    private const string SelfTestCommand = "--self-test";
    private const string CheckCommand = "--check";
    private const string Usage = "Use --self-test, --inventory ROOT or --check ROOT.";
    private const string Failure = "Code-quality analysis failed; no source contents are logged.";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };

    /// <summary>Return zero only after the requested operation succeeds; failures print safe metadata only.</summary>
    internal static int Run(string[] arguments)
    {
        try
        {
            if (arguments is [SelfTestCommand])
            {
                RuleControls.Run();
                return 0;
            }
            if (arguments is [InventoryCommand, var inventoryRoot])
            {
                Console.WriteLine(JsonSerializer.Serialize(RepositoryScan.Read(inventoryRoot), JsonOptions));
                return 0;
            }
            if (arguments is [CheckCommand, var root])
            {
                Allowances.Require(root, RepositoryScan.Read(root));
                return 0;
            }
            Console.Error.WriteLine(Usage);
            return 1;
        }
        catch (Exception exception) when (exception is IOException or JsonException or InvalidOperationException or ArgumentException)
        {
            Console.Error.WriteLine(Failure);
            return 1;
        }
    }
}
