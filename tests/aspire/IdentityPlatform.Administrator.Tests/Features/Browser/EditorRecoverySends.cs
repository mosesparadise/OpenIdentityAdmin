using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Counts actual browser mutation attempts without retaining headers, bodies, identifiers or request URLs.</summary>
internal sealed class EditorRecoverySends : IDisposable
{
    private const string Post = "POST";
    private const string Put = "PUT";
    private readonly IPage page;
    internal int Count
    {
        get; private set;
    }

    /// <summary>Attaches one passive request witness for an explicitly bounded scenario.</summary>
    internal EditorRecoverySends(IPage page)
    {
        this.page = page;
        page.Request += Observe;
    }

    /// <summary>Counts only the fixed management mutation family; proof acquisition and metadata reads cannot satisfy the witness.</summary>
    private void Observe(object? sender, IRequest request)
    {
        if (request.Method is Post or Put && new Uri(request.Url).AbsolutePath.StartsWith(EditorRecoveryView.Collection, StringComparison.Ordinal))
        {
            Count++;
        }
    }

    /// <summary>Removes passive observation without preserving any browser request object.</summary>
    public void Dispose() => page.Request -= Observe;
}
