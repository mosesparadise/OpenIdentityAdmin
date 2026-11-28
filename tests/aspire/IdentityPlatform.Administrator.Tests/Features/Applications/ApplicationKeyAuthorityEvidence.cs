using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using IdentityPlatform.Core.Features.Applications;
using IdentityPlatform.Infrastructure.Features.Applications;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using IdentityPlatform.Infrastructure.Persistence;

namespace IdentityPlatform.Administrator.Tests.Features.Applications;

/// <summary>Checks keyed creation and rotation using the real API's current permission, recent MFA and atomic audit workflow.</summary>
internal static class ApplicationKeyAuthorityEvidence
{
    private const string Machine = "keyed-machine";
    private const string Web = "keyed-web";
    private const string Name = "Synthetic signing client";
    private const string KeyId = "fixture-signing-key";
    private const int Bits = 3072;
    private const int Created = 201;
    private const int Updated = 200;
    private const int Forbidden = 403;
    private const int Stale = 412;
    private const int FreshnessBoundary = 300;
    private const string CredentialsPermission = "applications.credentials";
    private const string Callback = "https://portal.example.test/signin";
    private const string MachineProfile = "managed-machine-key-v1";
    private const string WebProfile = "managed-web-key-v1";

    /// <summary>Verifies both issuance profiles, a versioned replacement, and denial after recent MFA or current credential authority is lost.</summary>
    internal static async Task RequireAsync(ApplicationMutationFixture test)
    {
        var now = test.Clock.Now;
        var expiry = now.AddDays(1).ToString(ApplicationEditorRules.ExpiryFormat, CultureInfo.InvariantCulture);
        var pub = PublicKey();
        var machine = await test.ExecuteAsync(ApplicationMutationCommand.CreateWithKey(new ApplicationMachineCreate(Machine, Name, expiry), pub, now)!);
        Assert.Equal(Created, machine.Status);
        Assert.Equal(MachineProfile, machine.Application!.Profile);
        Assert.Null(machine.Credential);
        var web = await test.ExecuteAsync(ApplicationMutationCommand.CreateWithKey(new ApplicationCreate(Web, Name, [Callback], expiry), pub, now)!);
        Assert.Equal(Created, web.Status);
        Assert.Null(web.Credential);
        Assert.Equal(WebProfile, web.Application!.Profile);
        var replacement = ApplicationMutationCommand.ReplaceKey(machine.Application.Id, machine.Version!, PublicKey(), expiry, true, now)!;
        Assert.Equal(Updated, (await test.ExecuteAsync(replacement)).Status);
        Assert.Equal(Stale, (await test.ExecuteAsync(replacement)).Status);
        var more = ApplicationMutationCommand.ReplaceKey(web.Application.Id, web.Version!, PublicKey(), expiry, true, now)!;
        test.Clock.Now = now.AddSeconds(FreshnessBoundary);
        Assert.Equal(Forbidden, (await test.ExecuteAsync(more)).Status);
        test.Clock.Now = now;
        await using var scope = test.Scope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.True(await db.RoleClaims.Where(row => row.ClaimValue == CredentialsPermission).ExecuteDeleteAsync(test.Token) > 0);
        Assert.Equal(Forbidden, (await test.ExecuteAsync(more)).Status);
        var fresh = new ApplicationMachineCreate(Machine + Web, Name, expiry);
        Assert.Equal(Forbidden, (await test.ExecuteAsync(ApplicationMutationCommand.CreateWithKey(fresh, pub, now)!)).Status);
    }

    /// <summary>Exports only the six public RSA members consumed by the management command.</summary>
    private static string PublicKey()
    {
        using var rsa = RSA.Create(Bits);
        var parts = rsa.ExportParameters(false);
        const string Type = "RSA";
        const string Use = "sig";
        return JsonSerializer.Serialize(new
        {
            kty = Type,
            kid = KeyId,
            alg = SecurityAlgorithms.RsaSha256,
            use = Use,
            n = Base64UrlEncoder.Encode(parts.Modulus),
            e = Base64UrlEncoder.Encode(parts.Exponent)
        });
    }
}
