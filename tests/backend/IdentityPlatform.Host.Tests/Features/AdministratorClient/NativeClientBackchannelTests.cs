using System.Net;
using System.Net.Http.Headers;
using IdentityPlatform.Admin.Bff.Features.Authentication.Protocol;
using IdentityPlatform.Host.Tests.Features.ProtocolStorage;
using IdentityPlatform.Infrastructure.Features.ProtocolStorage;
using Microsoft.Extensions.Logging.Abstractions;
using OpenIddict.Abstractions;
using OpenIddict.Client;
using OpenIddict.Client.SystemNetHttp;

namespace IdentityPlatform.Host.Tests.Features.AdministratorClient;

/// <summary>Exercises real native HTTP serialization/sending with a test-only network sink and late external-context/message mutations.</summary>
public sealed class NativeClientBackchannelTests
{
    private const string Endpoint = "https://issuer.example/connect/par";
    private const string Foreign = "https://other.example/connect/par";
    private const string Client = "identity-admin-bff";
    private const string Assertion = "synthetic-unusable-assertion";
    private const string Changed = "synthetic-substitution";
    private const string Cookie = "Cookie";
    private const string Host = "other.example";
    private const string Basic = "Basic";

    /// <summary>Names independently reached late mutations after the outer request permit has already frozen its bindings.</summary>
    public enum Fault
    {
        RemoteUri, HttpUri, Method, Host, Authorization, Cookie, Fields, Duplicate, Request, NativeFields, Permit,
        DefaultAuthorization, DefaultCookie, DefaultHost, DefaultProxy
    }

    /// <summary>Actual native send completes once, including response loss, with no replay inside the permit.</summary>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Given_AdmittedNativeRequest_When_ActualSenderCompletesOrLosesResponse_Then_OnlyOneSendOccurs(bool loss)
    {
        await using var fixture = await SendFixture.CreateAsync(loss);
        await fixture.RunAsync(() => fixture.Sender.HandleAsync(fixture.Context));
        Assert.Equal(1, fixture.Network.Sends);
        Assert.Equal(loss, fixture.Context.IsRejected);
    }

    /// <summary>Each external-context or actual-message mutation is reached after permit creation and produces zero network sends.</summary>
    [Theory]
    [InlineData(Fault.RemoteUri)]
    [InlineData(Fault.HttpUri)]
    [InlineData(Fault.Method)]
    [InlineData(Fault.Host)]
    [InlineData(Fault.Authorization)]
    [InlineData(Fault.Cookie)]
    [InlineData(Fault.Fields)]
    [InlineData(Fault.Duplicate)]
    [InlineData(Fault.Request)]
    [InlineData(Fault.NativeFields)]
    [InlineData(Fault.Permit)]
    [InlineData(Fault.DefaultAuthorization)]
    [InlineData(Fault.DefaultCookie)]
    [InlineData(Fault.DefaultHost)]
    [InlineData(Fault.DefaultProxy)]
    public async Task Given_LateNativeMutation_When_FinalSenderInspectsActualMessage_Then_NoCredentialsAreSent(Fault fault)
    {
        await using var fixture = await SendFixture.CreateAsync(loss: false);
        var reached = false;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(async () =>
            {
                Change(fixture, fault);
                reached = true;
                await fixture.Sender.HandleAsync(fixture.Context);
            }));
        Assert.True(reached);
        Assert.Equal(0, fixture.Network.Sends);
    }

    /// <summary>Actual copied body size must reject legal individual fields whose declared length hides a total exceeding the bound.</summary>
    [Fact]
    public async Task Given_OversizedNativeForm_When_ContentLengthIsForged_Then_ActualBytesStillPreventSend()
    {
        const int FieldLength = 7000;
        const int ForgedLength = 100;
        const string First = "first";
        const string Second = "second";
        const string Third = "third";
        await using var fixture = await SendFixture.CreateAsync(loss: false);
        foreach (var field in new[] { First, Second, Third })
        {
            fixture.Request.SetParameter(field, new string('a', FieldLength));
        }
        fixture.Http.Content!.Dispose();
        await new OpenIddictClientSystemNetHttpHandlers.AttachHttpParameters<OpenIddictClientEvents.ApplyPushedAuthorizationRequestContext>()
            .HandleAsync(fixture.Context);
        fixture.Http.Content!.Headers.ContentLength = ForgedLength;
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(() => fixture.Sender.HandleAsync(fixture.Context)));
        Assert.Equal(0, fixture.Network.Sends);
    }

    /// <summary>A replacement ambient request cannot reuse a permit even with an otherwise unchanged native context and request.</summary>
    [Fact]
    public async Task Given_OriginalPermit_When_AmbientRequestIsReplaced_Then_NoSendOccurs()
    {
        await using var fixture = await SendFixture.CreateAsync(loss: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(async () =>
        {
            var original = ProtocolStorageAmbient.Current!;
            ProtocolStorageAmbient.Current = new(new ProtocolStorageRequest(TimeProvider.System, TestContext.Current.CancellationToken), original.Scope);
            try
            {
                await fixture.Sender.HandleAsync(fixture.Context);
            }
            finally { ProtocolStorageAmbient.Current = original; }
        }));
        Assert.Equal(0, fixture.Network.Sends);
    }

    /// <summary>The original monotonic request deadline is enforced even when native cancellation has not yet fired.</summary>
    [Fact]
    public async Task Given_OriginalPermit_When_RequestDeadlineExpiresBeforeSend_Then_NoSendOccurs()
    {
        await using var fixture = await SendFixture.CreateAsync(loss: false);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => fixture.RunAsync(async () =>
        {
            fixture.Expire();
            await fixture.Sender.HandleAsync(fixture.Context);
        }));
        Assert.Equal(0, fixture.Network.Sends);
    }

    /// <summary>An absent permit and a second invocation cannot cause another native network send.</summary>
    [Fact]
    public async Task Given_OneShotNativePermit_When_SendIsUnownedOrRepeated_Then_OnlyTheFirstOwnedSendIsPossible()
    {
        await using var fixture = await SendFixture.CreateAsync(loss: false);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.Sender.HandleAsync(fixture.Context).AsTask());
        Assert.Equal(0, fixture.Network.Sends);
        await Assert.ThrowsAsync<InvalidOperationException>(() => fixture.RunAsync(async () =>
            {
                await fixture.Sender.HandleAsync(fixture.Context);
                await fixture.Sender.HandleAsync(fixture.Context);
            }));
        Assert.Equal(1, fixture.Network.Sends);
    }

    /// <summary>Mutates either native external context or the already serialized HttpRequestMessage, preserving all other inputs.</summary>
    private static void Change(SendFixture fixture, Fault fault)
    {
        if (fault >= Fault.DefaultAuthorization)
        {
            ChangeDefaults(fixture, fault);
            return;
        }
        switch (fault)
        {
            case Fault.RemoteUri:
                fixture.Context.RemoteUri = new(Foreign);
                break;
            case Fault.HttpUri:
                fixture.Http.RequestUri = new(Foreign);
                break;
            case Fault.Method:
                fixture.Http.Method = HttpMethod.Get;
                break;
            case Fault.Host:
                fixture.Http.Headers.Host = Host;
                break;
            case Fault.Authorization:
                fixture.Http.Headers.Authorization = new AuthenticationHeaderValue(Basic, Changed);
                break;
            case Fault.Cookie:
                fixture.Http.Headers.Add(Cookie, Changed);
                break;
            case Fault.Fields:
                ReplaceContent(fixture, duplicate: false);
                break;
            case Fault.Duplicate:
                ReplaceContent(fixture, duplicate: true);
                break;
            case Fault.Request:
                fixture.Context.Request = new(fixture.Request.GetParameters());
                break;
            case Fault.NativeFields:
                fixture.Request.ClientAssertion = Changed;
                break;
            case Fault.Permit:
                fixture.Allowed = false;
                break;

        }
    }

    /// <summary>Changes headers that HttpClient merges after native message validation unless defaults are checked separately.</summary>
    private static void ChangeDefaults(SendFixture fixture, Fault fault)
    {
        switch (fault)
        {
            case Fault.DefaultAuthorization:
                fixture.Transport.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue(Basic, Changed);
                break;
            case Fault.DefaultCookie:
                fixture.Transport.DefaultRequestHeaders.Add(Cookie, Changed);
                break;
            case Fault.DefaultHost:
                fixture.Transport.DefaultRequestHeaders.Host = Host;
                break;
            case Fault.DefaultProxy:
                const string ProxyHeader = "Proxy-Authorization";
                fixture.Transport.DefaultRequestHeaders.Add(ProxyHeader, Changed);
                break;
        }
    }

    /// <summary>Replaces the native form with either altered credential bytes or a duplicate credential field.</summary>
    private static void ReplaceContent(SendFixture fixture, bool duplicate)
    {
        fixture.Http.Content!.Dispose();
        var fields = fixture.Request.GetParameters().Select(pair => KeyValuePair.Create(pair.Key, (string?)pair.Value)).ToList();
        if (!duplicate)
        {
            fields.RemoveAll(pair => pair.Key == OpenIddictConstants.Parameters.ClientAssertion);
        }
        fields.Add(KeyValuePair.Create<string, string?>(OpenIddictConstants.Parameters.ClientAssertion, Changed));
        fixture.Http.Content = new FormUrlEncodedContent(fields);
    }

    /// <summary>Owns a real native request/message/sender and substitutes only the network destination implementation.</summary>
    private sealed class SendFixture : IAsyncDisposable
    {
        internal NetworkSink Network
        {
            get;
        }
        internal OpenIddictRequest Request
        {
            get;
        }
        internal OpenIddictClientEvents.ApplyPushedAuthorizationRequestContext Context
        {
            get;
        }
        internal HttpRequestMessage Http
        {
            get;
        }
        internal bool Allowed { get; set; } = true;
        internal NativeClientHttpSend<OpenIddictClientEvents.ApplyPushedAuthorizationRequestContext> Sender
        {
            get;
        } =
            new(new OpenIddictClientSystemNetHttpHandlers.SendHttpRequest<OpenIddictClientEvents.ApplyPushedAuthorizationRequestContext>());
        internal readonly HttpClient Transport;
        private readonly StorageRequestProbe storage = new();

        /// <summary>Runs actual native form attachment, leaving the final sender and its error handling unchanged.</summary>
        private SendFixture(bool loss)
        {
            Network = new(loss);
            Transport = new(Network);
            Request = new()
            {
                ClientId = Client,
                ClientAssertion = Assertion,
                ClientAssertionType = OpenIddictConstants.ClientAssertionTypes.JwtBearer
            };
            var transaction = new OpenIddictClientTransaction
            {
                Request = Request,
                Registration = new() { ClientId = Client },
                Configuration = new(),
                Logger = NullLogger.Instance
            };
            Http = new(HttpMethod.Post, Endpoint);
            transaction.SetProperty(typeof(HttpRequestMessage).FullName!, Http);
            transaction.SetProperty(typeof(HttpClient).FullName!, Transport);
            Context = new(transaction)
            {
                RemoteUri = new(Endpoint)
            };
        }

        /// <summary>Completes native form serialization asynchronously before exposing the fixture to mutation controls.</summary>
        internal static async Task<SendFixture> CreateAsync(bool loss)
        {
            var fixture = new SendFixture(loss);
            try
            {
                await new OpenIddictClientSystemNetHttpHandlers.AttachHttpParameters<OpenIddictClientEvents.ApplyPushedAuthorizationRequestContext>()
                    .HandleAsync(fixture.Context);
                return fixture;
            }
            catch { await fixture.DisposeAsync(); throw; }
        }

        /// <summary>Uses an actual owned unopened native scope, preserving request identity without executing any database commands.</summary>
        internal Task<bool> RunAsync(Func<ValueTask> native) => new ProtocolStorageRequest(storage.Clock, TestContext.Current.CancellationToken)
            .RunAsync(storage.Factory, async (_, _) =>
            {
                await NativeClientBackchannel.RunAsync(Request, Context.Registration, Context.Configuration, new(Endpoint), Require, native);
                return true;
            });

        /// <summary>Advances only the actual request's monotonic clock to the exact ownership deadline.</summary>
        internal void Expire() => storage.Clock.Advance(ProtocolStorageLifetime.RequestLimit);

        /// <summary>Models an outer operation becoming invalid while nested preparation is still running.</summary>
        internal void Require()
        {
            if (!Allowed)
            {
                throw new InvalidOperationException();
            }
        }

        /// <summary>Disposes synthetic transport/output after native processing, including when a pre-send guard fails.</summary>
        public async ValueTask DisposeAsync()
        {
            Context.Transaction.GetHttpResponseMessage()?.Dispose();
            Http.Dispose();
            Transport.Dispose();
            await storage.DisposeAsync();
        }
    }

    /// <summary>Counts only actual HttpClient sends and never captures request credentials or body content.</summary>
    private sealed class NetworkSink(bool loss) : HttpMessageHandler
    {
        internal int Sends
        {
            get; private set;
        }

        /// <summary>Returns a synthetic status or drops the response without any automatic retry implementation.</summary>
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sends++;
            return loss ? Task.FromException<HttpResponseMessage>(new HttpRequestException()) :
                Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
