using Microsoft.Playwright;

namespace IdentityPlatform.Issuer.Tests.Features.Browser;

internal static class BrowserVisuals
{
    public static async Task CaptureAsync(IPage page, string name)
    {
        Assert.True(await page.Locator("input[name=Password], input[name=Username]").EvaluateAllAsync<bool>("elements => elements.every(element => element.value === '')"), "Captures must not contain credential values.");
        var root = Environment.GetEnvironmentVariable("IDENTITY_TEST_ARTIFACTS") ?? Path.Combine("artifacts", "tests", "browser");
        var path = Path.Combine(root, "screenshots", name + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var screenshot = await page.ScreenshotAsync(new()
        {
            Path = path,
            FullPage = true,
            Animations = ScreenshotAnimations.Disabled,
            Caret = ScreenshotCaret.Hide
        });
        // Semantic/axe/reflow checks run on every engine; pixel baselines are canonical Chromium only.
        if (BrowserSession.Engine != "chromium")
        {
            return;
        }
        var baselinePath = Path.Combine(AppContext.BaseDirectory, "Features", "Browser", "Baselines", name + ".png");
        Assert.True(File.Exists(baselinePath), "A captured visual baseline requires explicit review before acceptance.");
        var baseline = await File.ReadAllBytesAsync(baselinePath, TestContext.Current.CancellationToken);
        var difference = await page.EvaluateAsync<int[]>(ComparePixels, new[] { Convert.ToBase64String(baseline), Convert.ToBase64String(screenshot) });
        Assert.Equal(3, difference.Length);
        Assert.Equal(1, difference[0]);
        Assert.InRange(difference[1], 0, 1);
        Assert.InRange(difference[2], 0, 100);
    }

    // Decode pixels with the already-pinned browser. No image library or relaxed browser policy is required.
    // Native select-border rasterization differed by one channel unit at30 pixels across CI runners.
    private const string ComparePixels = """
        async images => {
            const decode = async base64 => {
                const bytes = Uint8Array.from(atob(base64), value => value.charCodeAt(0));
                const bitmap = await createImageBitmap(new Blob([bytes], {type: 'image/png'}));
                const canvas = new OffscreenCanvas(bitmap.width, bitmap.height);
                const context = canvas.getContext('2d', {willReadFrequently: true});
                context.drawImage(bitmap, 0, 0);
                const pixels = context.getImageData(0, 0, bitmap.width, bitmap.height).data;
                const result = {width: bitmap.width, height: bitmap.height, pixels};
                bitmap.close();
                return result;
            };
            const [expected, actual] = await Promise.all(images.map(decode));
            if (expected.width !== actual.width || expected.height !== actual.height)
                return [0, 255, 2147483647];
            let maxDelta = 0, changedPixels = 0;
            for (let index = 0; index < expected.pixels.length; index += 4) {
                let changed = false;
                for (let channel = 0; channel < 4; channel++) {
                    const delta = Math.abs(expected.pixels[index + channel] - actual.pixels[index + channel]);
                    maxDelta = Math.max(maxDelta, delta);
                    changed ||= delta !== 0;
                }
                if (changed) changedPixels++;
            }
            return [1, maxDelta, changedPixels];
        }
        """;

}
