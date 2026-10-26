using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using OpenIddict.Abstractions;
using OpenIddict.Client;

namespace IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;

/// <summary>Captures a server-owned browser demand before native state creation; this representation is not a durable reservation receipt.</summary>
internal sealed class NativeBrowserChallenge
{
    private static readonly string Key = Guid.NewGuid().ToString();
    private readonly OpenIddictClientEvents.ProcessChallengeContext original;
    private readonly BrowserLoginBinding binding;
    private readonly DateTimeOffset deadline;
    private readonly BrowserLoginReservation? reservation;
    internal Guid Correlation
    {
        get;
    }

    /// <summary>Retains immutable selectors and the original intent deadline without logging or publishing them into native state.</summary>
    private NativeBrowserChallenge(OpenIddictClientEvents.ProcessChallengeContext context, BrowserLoginBinding binding, DateTimeOffset deadline, Guid correlation, BrowserLoginReservation? reservation)
    {
        original = context;
        this.binding = binding;
        this.deadline = deadline;
        this.reservation = reservation;
        Correlation = correlation;
    }

    /// <summary>Accepts only explicit browser profile composition; callers must first reserve their durable intent in T010.</summary>
    internal static void Attach(OpenIddictClientEvents.ProcessChallengeContext context, NativeClientProfile profile,
        BrowserLoginBinding binding, DateTimeOffset originalDeadline, Guid correlation, BrowserLoginReservation? reservation = null)
    {
        var now = context.Options.TimeProvider!.GetUtcNow();
        if (correlation == Guid.Empty || !profile.Registration.IsBrowserSession || Find(context) is not null || originalDeadline <= now ||
            originalDeadline - now > TimeSpan.FromSeconds(AdministratorStateCapacity.LifetimeSeconds) ||
            context.Parameters.Keys.Any(IsBindingParameter))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        context.Parameters.Add(BrowserLoginBinding.IntentParameter, binding.Intent);
        context.Parameters.Add(BrowserLoginBinding.CompletionParameter, binding.Completion);
        context.Transaction.SetProperty(Key, new NativeBrowserChallenge(context, binding, originalDeadline, correlation, reservation));
    }

    /// <summary>Resolves only the original in-memory attempt, never an untrusted custom parameter or authentication property.</summary>
    internal static NativeBrowserChallenge? Find(OpenIddictClientEvents.ProcessChallengeContext context) =>
        context.Transaction.GetProperty<NativeBrowserChallenge>(Key);

    /// <summary>Recognizes aliases so unsupported presence cannot silently become ordinary B2.</summary>
    internal static bool IsBindingParameter(string key) => string.Equals(key, BrowserLoginBinding.IntentParameter, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(key, BrowserLoginBinding.CompletionParameter, StringComparison.OrdinalIgnoreCase);

    /// <summary>Checks immutable values and strict original lifetime before state persistence and around native PAR transport.</summary>
    internal bool Matches(OpenIddictClientEvents.ProcessChallengeContext context) => ReferenceEquals(original, context) &&
        (reservation is null || reservation.Current(context)) &&
        deadline > context.Options.TimeProvider!.GetUtcNow() &&
        context.Parameters.Where(pair => IsBindingParameter(pair.Key)).Count() == BindingFields &&
        context.Parameters.TryGetValue(BrowserLoginBinding.IntentParameter, out var intent) && Scalar(intent) == binding.Intent &&
        context.Parameters.TryGetValue(BrowserLoginBinding.CompletionParameter, out var completion) && Scalar(completion) == binding.Completion;

    /// <summary>Stores only the digest and clamps the actual native state to the original intent deadline before its existing durable owner runs.</summary>
    internal void Prepare(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        if (!Matches(context) || context.StateTokenPrincipal is not { } state || state.GetExpirationDate() is not { } expires ||
            state.Claims.Any(claim => string.Equals(claim.Type, BrowserSessionIntent.ClaimType, StringComparison.OrdinalIgnoreCase)))
        {
            throw ProtocolStorageOwner.Unavailable();
        }
        state.SetClaim(BrowserSessionIntent.ClaimType, binding.IntentDigest);
        reservation?.Project(state);
        state.SetExpirationDate(expires < deadline ? expires : deadline);
    }

    /// <summary>Prevents later handlers from replacing the protected digest, extending the deadline or retaining raw selector claims.</summary>
    internal bool StateMatches(OpenIddictClientEvents.ProcessChallengeContext context) => Matches(context) &&
        context.StateTokenPrincipal is { } state && BrowserSessionIntent.Read(state) == binding.IntentDigest &&
        BrowserLoginBinding.Read(state, out _) == BrowserLoginBindingState.Absent &&
        (reservation is null ? BrowserLoginReservation.Absent(state) : reservation.Matches(state)) &&
        state.GetExpirationDate() is { } expires && expires <= deadline;

    /// <summary>Ordinary challenges cannot smuggle browser expectations or selectors into protected native state without a captured attempt.</summary>
    internal static bool UnboundState(OpenIddictClientEvents.ProcessChallengeContext context) => context.StateTokenPrincipal is { } state &&
        BrowserLoginReservation.Absent(state) &&
        BrowserLoginBinding.Read(state, out _) == BrowserLoginBindingState.Absent &&
        !state.Claims.Any(claim => string.Equals(claim.Type, BrowserSessionIntent.ClaimType, StringComparison.OrdinalIgnoreCase));

    /// <summary>Requires the native string shape; arrays or coercible JSON scalars are never accepted as selectors.</summary>
    private static string? Scalar(OpenIddictParameter value) => (System.Text.Json.JsonElement)value is
    { ValueKind: System.Text.Json.JsonValueKind.String } json ? json.GetString() : null;
    private const int BindingFields = 2;
}

/// <summary>Adds a protected browser expectation only to an explicitly captured native challenge.</summary>
internal sealed class NativeBrowserState : IOpenIddictClientHandler<OpenIddictClientEvents.ProcessChallengeContext>
{
    /// <summary>Runs immediately after native state preparation; ordinary B2 challenges remain untouched.</summary>
    public ValueTask HandleAsync(OpenIddictClientEvents.ProcessChallengeContext context)
    {
        NativeBrowserChallenge.Find(context)?.Prepare(context);
        return ValueTask.CompletedTask;
    }
}
