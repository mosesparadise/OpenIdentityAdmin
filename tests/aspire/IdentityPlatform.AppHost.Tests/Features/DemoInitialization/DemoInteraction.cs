using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;

namespace IdentityPlatform.AppHost.Tests.Features.DemoInitialization;

/// <summary>Supplies controlled dashboard inputs while preserving the production native command and transport.</summary>
internal sealed class DemoInteraction : IInteractionService
{
    private const string UsernameField = "username";
    private const string PasswordField = "password";
    private const int InputCount = 2;
    private const int PasswordLimit = 256;

    internal const string User = "synthetic-demo-admin";
    internal const string Secret = "Aa1!synthetic-canary-password";
    public bool IsAvailable { get; set; } = true;
    internal string Username { get; set; } = User;
    internal string Password { get; set; } = Secret;
    internal bool Cancel
    {
        get; set;
    }
    internal int Prompts
    {
        get; private set;
    }
    internal Func<CancellationToken, Task>? BeforeReturn
    {
        get; set;
    }
    internal IReadOnlyList<InteractionInput> LastInputs { get; private set; } = [];

    /// <summary>Requires a fresh masked prompt with no defaults, then returns the controlled synthetic values.</summary>
    public async Task<InteractionResult<InteractionInputCollection>> PromptInputsAsync(string title, string? message, IReadOnlyList<InteractionInput> inputs, InputsDialogInteractionOptions? options = null, CancellationToken cancellationToken = default)
    {
        Prompts++;
        LastInputs = inputs;
        Assert.Equal(InputCount, inputs.Count);
        Assert.All(inputs, input => Assert.True(string.IsNullOrEmpty(input.Value)));
        var user = Assert.Single(inputs, input => input.Name == UsernameField);
        var password = Assert.Single(inputs, input => input.Name == PasswordField);
        Assert.Equal(InputType.SecretText, password.InputType);
        Assert.True(password.Required);
        Assert.Equal(PasswordLimit, password.MaxLength);
        if (BeforeReturn is not null)
        {
            await BeforeReturn(cancellationToken);
        }
        if (Cancel)
        {
            return InteractionResult.Cancel<InteractionInputCollection>();
        }
        user.Value = Username;
        password.Value = Password;
        return InteractionResult.Ok(new InteractionInputCollection(inputs));
    }

    /// <summary>Rejects any unexpected confirmation interaction.</summary>
    public Task<InteractionResult<bool>> PromptConfirmationAsync(string title, string message, MessageBoxInteractionOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <summary>Rejects any unexpected message box interaction.</summary>
    public Task<InteractionResult<bool>> PromptMessageBoxAsync(string title, string message, MessageBoxInteractionOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <summary>Rejects an unmasked legacy single input prompt.</summary>
    public Task<InteractionResult<InteractionInput>> PromptInputAsync(string title, string? message, string inputLabel, string placeHolder, InputsDialogInteractionOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <summary>Rejects unexpected single input prompts.</summary>
    public Task<InteractionResult<InteractionInput>> PromptInputAsync(string title, string? message, InteractionInput input, InputsDialogInteractionOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <summary>Rejects notifications that might accidentally include submitted secrets.</summary>
    public Task<InteractionResult<bool>> PromptNotificationAsync(string title, string message, NotificationInteractionOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
#pragma warning disable ASPIREINTERACTION001, ASPIRETERMINAL001 // Required interface stubs only; both reject use. Re-review when Aspire changes.
    /// <summary>Rejects progress interactions outside the specified command contract.</summary>
    public Task<InteractionResult<bool>> PromptProgressAsync(string message, ProgressInteractionOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    /// <summary>Rejects terminal exposure of bootstrap stdin.</summary>
    public Task<InteractionResult<bool>> PromptTerminalAsync(string message, AspireTerminal terminal, TerminalInteractionOptions? options = null, CancellationToken cancellationToken = default) => throw new NotSupportedException();
#pragma warning restore ASPIREINTERACTION001, ASPIRETERMINAL001
}
