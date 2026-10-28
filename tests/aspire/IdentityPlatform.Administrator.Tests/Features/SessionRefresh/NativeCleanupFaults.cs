using System.Text.Json;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.WebUtilities;
using OpenIddict.Abstractions;

namespace IdentityPlatform.Administrator.Tests.Features.SessionRefresh;

/// <summary>Provides exact causal test faults and bounded reach observations without retaining protocol response material.</summary>
internal sealed class NativeCleanupFaults
{
    private const string RollbackMessage = "Injected native cleanup precommit rollback.";
    private const string DuplicateMessage = "Injected native cleanup duplicate binding rejection.";
    internal Exception Rollback { get; } = new InvalidOperationException(RollbackMessage);
    internal Exception Duplicate { get; } = new InvalidOperationException(DuplicateMessage);
    internal int RollbackHits
    {
        get; private set;
    }
    internal int DuplicateHits
    {
        get; private set;
    }
    internal bool StagedConfirmation
    {
        get; private set;
    }

    /// <summary>Requires successful native dispatch before injecting rollback, so setup/authentication failures cannot satisfy a fault test.</summary>
    internal void ThrowRollback<T>(T result)
    {
        var response = Assert.IsType<NativeCleanupResponse>(result);
        Assert.True(response.Status is StatusCodes.Status200OK or StatusCodes.Status302Found);
        if (response.Status == StatusCodes.Status302Found)
        {
            var uri = new Uri(response.Location);
            Assert.True(uri.GetLeftPart(UriPartial.Path) == NativeCleanupProfile.Callback);
            var query = QueryHelpers.ParseQuery(uri.Query);
            Assert.False(query.ContainsKey(OpenIddictConstants.Parameters.Error));
            Assert.True(query.TryGetValue(OpenIddictConstants.Parameters.Code, out var code) && code.Count == 1 && !string.IsNullOrEmpty(code[0]));
        }
        if (response.Status == StatusCodes.Status200OK)
        {
            using var json = JsonDocument.Parse(response.Body);
            StagedConfirmation = json.RootElement.TryGetProperty(NativeCleanupProfile.Confirmation, out var value) && value.GetBoolean();
        }
        RollbackHits++;
        throw Rollback;
    }

    /// <summary>Identifies the actual duplicate-selector branch after native authorization attachment.</summary>
    internal void ThrowDuplicate()
    {
        DuplicateHits++;
        throw Duplicate;
    }

    /// <summary>Recognizes only the fixture-owned causal exception, including wrappers added by native transport or owned cleanup.</summary>
    internal static bool CausedBy(Exception? actual, Exception expected) => ReferenceEquals(actual, expected) ||
        actual is AggregateException aggregate && aggregate.InnerExceptions.Any(value => CausedBy(value, expected)) ||
        actual?.InnerException is { } inner && CausedBy(inner, expected);
}
