using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using IdentityPlatform.Admin.Bff.Features.Applications;
using IdentityPlatform.Admin.Bff.Features.BrowserSessions;
using IdentityPlatform.Core.Features.Applications;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.DependencyInjection;

namespace IdentityPlatform.Host.Tests.Features.Applications;

/// <summary>Checks the assignment transport against independently substituted success envelopes without allowing retries or browser credential forwarding.</summary>
public sealed class ApplicationScopesTransportTests
{
    private const string Origin = "https://admin.example.test";
    private const string Issuer = "https://issuer.example.test/";
    private const string CsrfProof = "synthetic-native-proof";
    private const string Id = "scope-transport-target";
    private const string Other = "foreign-scope-target";
    private const string Path = "/bff/api/v1/applications/scope-transport-target/scopes";
    private const string Target = "https://api.example.test/api/v1/applications";
    private const string Body = "{\"scopes\":[\"email\"]}";
    private const string Json = "application/json";
    private const string Email = "email";
    private const string Orders = "orders.read";
    private const string Secret = "synthetic-server-token";
    private const int Successor = 2;
    private const int SkippedRevision = 3;
    private static readonly DateTimeOffset Now = new(2026, 11, 8, 0, 0, 0, TimeSpan.Zero);

    private const string Separator = "/";
    private const string Suffix = "/scopes";
    private const string Bearer = "Bearer";
    private const string Cookie = "Cookie";
    private const string Csrf = "X-CSRF-TOKEN";
    private const string ExtraField = "\"private\":true,";

    /// <summary>Every substituted identity, tag, revision, assignment set, UTC offset or framing defect fails closed after exactly one upstream send.</summary>
    [Theory]
    [InlineData(ScopeResponseFault.None)]
    [InlineData(ScopeResponseFault.ForeignId)]
    [InlineData(ScopeResponseFault.ForeignTag)]
    [InlineData(ScopeResponseFault.OldRevision)]
    [InlineData(ScopeResponseFault.FutureRevision)]
    [InlineData(ScopeResponseFault.Substitution)]
    [InlineData(ScopeResponseFault.Duplicate)]
    [InlineData(ScopeResponseFault.Offset)]
    [InlineData(ScopeResponseFault.Redirect)]
    [InlineData(ScopeResponseFault.UnknownField)]
    public async Task Given_OneAssignmentIntent_When_UpstreamSuccessIsSubstituted_Then_OnlyExactBoundSuccessCanPublish(ScopeResponseFault fault)
    {
        var input = await MutationAsync();
        using var handler = new ResponseHandler(fault);
        using var client = new HttpClient(handler);
        var access = BrowserAccessCredential.Capture(Secret, Now.AddMinutes(1), Now)!;
        if (fault == ScopeResponseFault.None)
        {
            var result = await ApplicationScopesTransport.SendAsync(client, new(Target), access, Id, input, TestContext.Current.CancellationToken);
            Assert.Equal(200, result.Status);
            Assert.Equal([Email], result.Output!.Scopes);
            Assert.Equal(ApplicationVersion.Create(Id, Successor), result.Version);
        }
        else if (fault == ScopeResponseFault.UnknownField)
        {
            await Assert.ThrowsAsync<JsonException>(() => ApplicationScopesTransport.SendAsync(client, new(Target), access, Id, input, TestContext.Current.CancellationToken));
        }
        else
        {
            await Assert.ThrowsAsync<InvalidDataException>(() => ApplicationScopesTransport.SendAsync(client, new(Target), access, Id, input, TestContext.Current.CancellationToken));
        }
        Assert.Equal(1, handler.Count);
    }

    /// <summary>Uses the production strict request parser to freeze one canonical browser intent.</summary>
    private static async Task<ApplicationMutationRequest> MutationAsync()
    {
        using var services = new ServiceCollection().AddSingleton(new BrowserSessionDestinations(Issuer, Origin + Separator)).BuildServiceProvider();
        var http = new DefaultHttpContext { RequestServices = services };
        http.Request.Headers.Origin = Origin;
        http.Request.Headers[Csrf] = CsrfProof;
        http.Request.Scheme = Uri.UriSchemeHttps;
        http.Request.Method = HttpMethods.Put;
        http.Request.Path = Path;
        http.Features.Get<IHttpRequestFeature>()!.RawTarget = Path;
        http.Request.ContentType = Json;
        http.Request.Headers.IfMatch = ApplicationVersion.Create(Id, 1);
        http.Request.Body = new MemoryStream(Encoding.UTF8.GetBytes(Body));
        http.Request.ContentLength = http.Request.Body.Length;
        return await ApplicationMutationRequest.ReadAsync(http, ApplicationProxyAction.AssignScopes, Id, Now);
    }

    /// <summary>Returns a single finite upstream result while independently asserting exact server-only forwarding.</summary>
    private sealed class ResponseHandler(ScopeResponseFault fault) : HttpMessageHandler
    {
        internal int Count
        {
            get;
            private set;
        }

        /// <summary>Checks the actual outbound request before changing only the selected response property.</summary>
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            Assert.Equal(HttpMethod.Put, request.Method);
            Assert.Equal(Target + Separator + Id + Suffix, request.RequestUri!.AbsoluteUri);
            Assert.Equal(Secret, request.Headers.Authorization!.Parameter);
            Assert.Equal(Bearer, request.Headers.Authorization.Scheme);
            Assert.Equal(ApplicationVersion.Create(Id, 1), Assert.Single(request.Headers.IfMatch).ToString());
            Assert.False(request.Headers.Contains(Cookie));
            Assert.False(request.Headers.Contains(Csrf));
            Assert.Equal(Body, await request.Content!.ReadAsStringAsync(cancellationToken));
            var output = new ApplicationScopes(fault == ScopeResponseFault.ForeignId ? Other : Id,
                fault == ScopeResponseFault.Substitution ? [Orders] : fault == ScopeResponseFault.Duplicate ? [Email, Email] : [Email],
                fault == ScopeResponseFault.Offset ? Now.AddMinutes(1).ToOffset(TimeSpan.FromHours(1)) : Now.AddMinutes(1));
            var body = JsonSerializer.Serialize(output, JsonSerializerOptions.Web);
            if (fault == ScopeResponseFault.UnknownField)
            {
                body = body.Insert(1, ExtraField);
            }
            var response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(body, Encoding.UTF8, Json) };
            response.Headers.ETag = new EntityTagHeaderValue(ApplicationVersion.Create(fault == ScopeResponseFault.ForeignTag ? Other : Id,
                fault == ScopeResponseFault.OldRevision ? 1 : fault == ScopeResponseFault.FutureRevision ? SkippedRevision : Successor));
            if (fault == ScopeResponseFault.Redirect)
            {
                response.Headers.Location = new(Target);
            }
            return response;
        }
    }
}

/// <summary>Names independently changed transport fields whose correctness cannot be inferred from a successful status alone.</summary>
public enum ScopeResponseFault
{
    None, ForeignId, ForeignTag, OldRevision, FutureRevision, Substitution, Duplicate, Offset, Redirect, UnknownField
}
