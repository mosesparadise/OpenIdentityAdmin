using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Exercises native keyboard clipboard transfer from a separate test-owned page and records only trusted-event booleans.</summary>
internal static class AdministratorClipboard
{
    private const string SourceHtml = "<textarea aria-label='Synthetic clipboard source'></textarea>";
    private const string Textarea = "textarea";
    private const string SelectAll = "ControlOrMeta+A";
    private const string Copy = "ControlOrMeta+C";
    private const string Paste = "ControlOrMeta+V";
    private const string ObservePaste = "element => { element.dataset.nativePaste = 'false'; element.addEventListener('paste', event => { element.dataset.nativePaste = String(event.isTrusted); }, { once: true }); }";
    private const string TrustedPaste = "element => element.dataset.nativePaste === 'true'";
    private const string Cleared = "synthetic-clipboard-cleared";
    private const string Failure = "Native clipboard transfer did not produce trusted paste of the expected value.";

    /// <summary>Copies through the engine's real clipboard, pastes through native keyboard input, and overwrites clipboard contents afterward.</summary>
    internal static async Task PasteAsync(IPage target, string selector, string value)
    {
        var source = await target.Context.NewPageAsync();
        try
        {
            await source.SetContentAsync(SourceHtml);
            await CopyAsync(source, value);
            await target.BringToFrontAsync();
            var input = target.Locator(selector);
            await input.EvaluateAsync(ObservePaste);
            await input.FocusAsync();
            await input.PressAsync(Paste);
            Assert.True(await input.EvaluateAsync<bool>(TrustedPaste) && await input.InputValueAsync() == value, Failure);
        }
        finally
        {
            try
            {
                await CopyAsync(source, Cleared);
                var text = source.Locator(Textarea);
                await text.FillAsync(string.Empty);
                await text.PressAsync(Paste);
                Assert.True(await text.InputValueAsync() == Cleared, Failure);
            }
            finally { await source.CloseAsync(); await target.BringToFrontAsync(); }
        }
    }

    /// <summary>Fills only the test-owned source textarea; the production target receives data exclusively through native paste.</summary>
    private static async Task CopyAsync(IPage source, string value)
    {
        await source.BringToFrontAsync();
        var text = source.Locator(Textarea);
        await text.FillAsync(value);
        await text.PressAsync(SelectAll);
        await text.PressAsync(Copy);
    }
}
