namespace IdentityPlatform.AuthServer.Features.Accounts;

public static class AccountAssets
{
    public static void MapAccountAssets(this WebApplication app)
    {
        foreach (var name in new[] { "tokens.css", "theme.js", "login.css", "login.js" })
        {
            using var source = typeof(AccountAssets).Assembly.GetManifestResourceStream("IdentityPlatform.AccountAssets." + name)
                ?? throw new InvalidOperationException("A required account asset is unavailable.");
            using var buffer = new MemoryStream();
            source.CopyTo(buffer);
            var bytes = buffer.ToArray();
            var contentType = name.EndsWith(".css", StringComparison.Ordinal) ? "text/css; charset=utf-8" : "text/javascript; charset=utf-8";
            app.MapGet("/assets/" + name, () => Results.Bytes(bytes, contentType));
        }
    }
}
