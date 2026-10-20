using IdentityPlatform.Infrastructure.Features.ProtocolStorage.Administrator;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Server;

namespace IdentityPlatform.Host.Tests.Features.ProtocolStorage;

/// <summary>Distinguishes an opaque issued audience from an optional standards-compliant request resource indicator.</summary>
public sealed class NativeResourceSyntaxTests
{
    private const string AbsoluteResource = "https://other.example.test/resource";

    /// <summary>The installed native syntax validator rejects the opaque audience as input; omission leaves resource mapping to the server.</summary>
    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData(AdministratorProtocolIdentity.Resource, true)]
    [InlineData(AbsoluteResource, false)]
    public async Task Given_ResourceIndicator_When_NativeParSyntaxRuns_Then_OpaqueAudienceCannotBeUsedAsRequestResource(string? resource, bool rejected)
    {
        var context = new OpenIddictServerEvents.ValidatePushedAuthorizationRequestContext(new()
        {
            Request = new(),
            Logger = NullLogger.Instance
        });
        if (resource is not null)
        {
            context.Request.SetParameter(OpenIddictConstants.Parameters.Resource, resource);
        }
        await new OpenIddictServerHandlers.Authentication.ValidatePushedResourceParameter().HandleAsync(context);
        Assert.Equal(rejected, context.IsRejected);
        Assert.Equal(rejected ? OpenIddictConstants.Errors.InvalidRequest : null, context.Error);
    }
}
