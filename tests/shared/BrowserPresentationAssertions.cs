using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

namespace IdentityPlatform.Testing;

/// <summary>Pins the reviewed theme-only script while rejecting additional script or event-handler navigation and mutation mechanisms.</summary>
internal static class BrowserPresentationAssertions
{
    private const string ThemeHash = "w+uuhquW+ZQV55tUvccOSS+TKS9Hbuz6bhtay55K5ho=";
    private const string Script = "<script\\b([^>]*)>(.*?)</script>";
    private const string Handler = "\\son[a-z]+\\s*=";
    private const string ScriptScheme = "javascript:";
    private const int BodyGroup = 2;
    private const int RegexSeconds = 1;

    /// <summary>Accepts exactly the immutable canonical theme script; its pinned bytes contain no authentication, navigation or network action.</summary>
    internal static void ThemeOnly(string html)
    {
        var scripts = Regex.Matches(html, Script, RegexOptions.CultureInvariant | RegexOptions.Singleline | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(RegexSeconds));
        var count = scripts.Count;
        Xunit.Assert.Equal(1, count);
        Xunit.Assert.True(string.IsNullOrWhiteSpace(scripts[0].Groups[1].Value));
        var digest = Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(scripts[0].Groups[BodyGroup].Value)));
        Xunit.Assert.True(digest == ThemeHash);
        Xunit.Assert.False(Regex.IsMatch(html, Handler, RegexOptions.CultureInvariant | RegexOptions.IgnoreCase, TimeSpan.FromSeconds(RegexSeconds)));
        Xunit.Assert.False(html.Contains(ScriptScheme, StringComparison.OrdinalIgnoreCase));
    }
}
