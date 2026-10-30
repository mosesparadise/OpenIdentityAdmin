using IdentityPlatform.Admin.Api.Features.Applications;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Proves the actual API serializer cannot publish bytes or an aggregate version after the final authority observation expires.</summary>
public sealed class ApplicationPublicationTests
{
    private const string Identity = "application-id";

    /// <summary>Changes authority from inside a real serialized property and requires the final check to suppress the entire staged response.</summary>
    [Fact]
    public async Task Given_ApiAuthorityExpiresDuringSerialization_When_ResponseIsPublished_Then_NoBodyOrVersionEscapes()
    {
        var current = true;
        var http = new DefaultHttpContext();
        http.Response.Body = new MemoryStream();
        using var budget = new ApplicationReadBudget(TimeProvider.System, TestContext.Current.CancellationToken);
        var value = new SerializationExpiry(() => current = false);
        var error = await Assert.ThrowsAsync<ApplicationHttpRejected>(() => ApplicationHttpBoundary.PublishAsync(http, value,
            StatusCodes.Status201Created, ApplicationVersion.Create(Identity, 1), budget, () => current));
        Assert.Equal(StatusCodes.Status401Unauthorized, error.Status);
        Assert.Equal(0, http.Response.Body.Length);
        Assert.Equal(0, http.Response.Headers.ETag.Count);
    }

    /// <summary>Changes authority only when the real serializer reads a public property.</summary>
    private sealed class SerializationExpiry(Action expire)
    {
        public bool Value
        {
            /// <summary>Expires the observation during serialization instead of pre-expiring the test setup.</summary>
            get
            {
                expire();
                return true;
            }
        }
    }
}
