using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Diagnostic-branch-only geometry experiment; restores every style/value and never changes the original failing acceptance assertion.</summary>
internal static class CatalogueLayoutProbe
{
    private const string Probe = """
        async () => {
          const select = document.querySelector('#catalogue-audienceId');
          if (!select) return 'catalogue-layout-probe: absent';
          const parent = select.parentElement, options = [...select.options];
          const initial = { select: select.style.cssText, parent: parent.style.cssText, options: options.map(o => o.textContent) };
          const frames = () => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve)));
          const tests = [
            ['settle-only', () => {}],
            ['select-contain-paint', () => select.style.contain = 'paint'],
            ['select-appearance-none', () => select.style.appearance = 'none'],
            ['select-contain-inline-size', () => select.style.contain = 'inline-size'],
            ['parent-overflow-hidden', () => parent.style.overflow = 'hidden'],
            ['parent-contain-paint', () => parent.style.contain = 'paint'],
            ['parent-flow-root', () => parent.style.display = 'flow-root'],
            ['short-option-labels', () => options.forEach(o => o.textContent = 'Selected audience')],
            ['option-max-width', () => options.forEach(o => o.style.maxWidth = '100%')]
          ];
          const results = [{ name: 'original', scrollWidth: document.documentElement.scrollWidth }];
          for (const [name, apply] of tests) {
            const styles = options.map(o => o.style.cssText);
            apply(); await frames();
            results.push({ name, scrollWidth: document.documentElement.scrollWidth, selectWidth: select.getBoundingClientRect().width, parentScrollWidth: parent.scrollWidth });
            select.style.cssText = initial.select; parent.style.cssText = initial.parent;
            options.forEach((o, i) => { o.textContent = initial.options[i]; o.style.cssText = styles[i]; });
            await frames();
          }
          results.push({ name: 'restored', scrollWidth: document.documentElement.scrollWidth });
          return JSON.stringify({ diagnosticOnly: true, viewport: innerWidth, results });
        }
        """;

    /// <summary>Returns only finite case names and numeric geometry; no account, credential, option text or authenticated pixels are retained.</summary>
    internal static Task<string> ReadAsync(IPage page) => page.EvaluateAsync<string>(Probe);
}
