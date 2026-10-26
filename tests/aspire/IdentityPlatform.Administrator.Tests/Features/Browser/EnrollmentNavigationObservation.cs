using System.Globalization;
using System.Text;
using Microsoft.Playwright;

namespace IdentityPlatform.Administrator.Tests.Features.Browser;

/// <summary>Retains only finite navigation facts around the unchanged native acknowledgement, never URLs, cookie values or error text.</summary>
internal sealed class EnrollmentNavigationObservation : IDisposable
{
    private const string SessionCookie = "__Host-identity-administrator";
    private const string Href = "href";
    private const string Root = "/";
    private static readonly CompositeFormat Report = CompositeFormat.Parse("enrollment-ack requests={0} responses={1} status={2} failures={3} committed={4} dom={5} loaded={6} final-target={7}");
    private readonly IPage page;
    private readonly Uri target;
    private int requests;
    private int responses;
    private int status;
    private int failures;
    private bool committed;
    private bool dom;
    private bool loaded;

    /// <summary>Attaches synchronous scalar observers before the original single browser action.</summary>
    private EnrollmentNavigationObservation(IPage page, Uri target)
    {
        this.page = page;
        this.target = target;
        page.Request += Request;
        page.Response += Response;
        page.RequestFailed += Failed;
        page.FrameNavigated += Navigated;
        page.DOMContentLoaded += Dom;
        page.Load += Loaded;
    }

    /// <summary>Checks the actual link and applicable cookie scope privately, then arms observers without issuing another request.</summary>
    internal static async Task<EnrollmentNavigationObservation> StartAsync(IPage page, Uri target, string linkName)
    {
        var link = page.GetByRole(AriaRole.Link, new()
        {
            Name = linkName,
            Exact = true
        });
        Assert.Equal(1, await link.CountAsync());
        Assert.True(Uri.TryCreate(new Uri(page.Url), await link.GetAttributeAsync(Href), out var destination) && destination == target);
        var cookies = (await page.Context.CookiesAsync([target.AbsoluteUri])).Where(cookie => cookie.Name == SessionCookie).ToArray();
        var cookiesCount = cookies.Length;
        Assert.Equal(1, cookiesCount);
        Assert.True(cookies[0].Domain == target.Host && cookies[0].Path == Root && cookies[0].HttpOnly && cookies[0].Secure);
        return new(page, target);
    }

    /// <summary>Matches only the exact expected main-document navigation while retaining no request objects.</summary>
    private bool Matches(IRequest request) => request.IsNavigationRequest && request.Frame == page.MainFrame && Matches(request.Url);

    /// <summary>Compares the private runtime address without rendering it in any diagnostic.</summary>
    private bool Matches(string value) => Uri.TryCreate(value, UriKind.Absolute, out var uri) && uri == target;

    /// <summary>Counts the exact request.</summary>
    private void Request(object? sender, IRequest request)
    {
        if (Matches(request))
        {
            requests++;
        }
    }

    /// <summary>Retains only the native status of the exact main-document response.</summary>
    private void Response(object? sender, IResponse response)
    {
        if (Matches(response.Request))
        {
            responses++;
            status = response.Status;
        }
    }

    /// <summary>Counts network failure without retaining browser error text.</summary>
    private void Failed(object? sender, IRequest request)
    {
        if (Matches(request))
        {
            failures++;
        }
    }

    /// <summary>Records whether the expected destination actually committed.</summary>
    private void Navigated(object? sender, IFrame frame)
    {
        if (frame == page.MainFrame && Matches(frame.Url))
        {
            committed = true;
        }
    }

    /// <summary>Records the expected document's DOM milestone.</summary>
    private void Dom(object? sender, IPage value)
    {
        if (Matches(value.Url))
        {
            dom = true;
        }
    }

    /// <summary>Records the expected document's original load milestone.</summary>
    private void Loaded(object? sender, IPage value)
    {
        if (Matches(value.Url))
        {
            loaded = true;
        }
    }

    /// <summary>Detaches before reporting scalar facts; a diagnostic sink cannot replace the original test failure.</summary>
    public void Dispose()
    {
        page.Request -= Request;
        page.Response -= Response;
        page.RequestFailed -= Failed;
        page.FrameNavigated -= Navigated;
        page.DOMContentLoaded -= Dom;
        page.Load -= Loaded;
        try
        {
            TestContext.Current.TestOutputHelper?.WriteLine(string.Format(CultureInfo.InvariantCulture, Report,
                requests, responses, status, failures, committed, dom, loaded, Matches(page.Url)));
        }
        catch (Exception) { /* Preserve the original browser outcome. */ }
    }
}
