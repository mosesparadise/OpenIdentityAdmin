using System.Text;
using IdentityPlatform.Sample.Client.Features.Configuration;

namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Tests private one-shot setup parsing and atomic publication using disposable synthetic inputs.</summary>
public sealed class SampleConfigurationTests
{
    private const string ClientIdSample1ClientSecretSyntheticPrivateText = "{\"clientId\":\"sample._-1\",\"clientSecret\":\"synthetic-private\"}";
    private const int OversizedInputBytes = 4097;
    private const string TemporaryDirectoryPrefix = "sample024-";
    private const string CompactGuidFormat = "N";
    private const string ConfigurationFilename = "sample.json";
    private const string FirstText = "first";
    private const string SecondText = "second";
    private const string ThirdText = "third";
    private const string ClientIdText = "{\"clientId\":\"";
    private const string ClientSecretSyntheticText = "\",\"clientSecret\":\"synthetic\"}";

    /// <summary>Ambiguous, extra, malformed or oversized stdin never becomes usable native configuration.</summary>
    [Theory]
    [InlineData("{}")]
    [InlineData("[]")]
    [InlineData("{\"clientId\":\"x\",\"clientSecret\":\"x\",\"issuer\":\"https://wrong.invalid\"}")]
    [InlineData("{\"clientId\":\"x\",\"clientId\":\"y\",\"clientSecret\":\"x\"}")]
    [InlineData("{\"clientId\":\"../x\",\"clientSecret\":\"x\"}")]
    [InlineData("{\"clientId\":\"x\",\"clientSecret\":\"\"}")]
    [InlineData("{\"clientId\":\"x\",\"clientSecret\":1}")]
    public void Given_InvalidStdin_When_Parsed_Then_ConfigurationIsRejected(string json) =>
        Assert.ThrowsAny<Exception>(() => SampleSettings.Parse(Encoding.UTF8.GetBytes(json)));

    /// <summary>Only a bounded immutable id/secret pair is accepted; string rendering never discloses the private secret.</summary>
    [Fact]
    public void Given_BoundedPair_When_Parsed_Then_ExactValuesRemainPrivate()
    {
        var settings = SampleSettings.Parse(Encoding.UTF8.GetBytes(ClientIdSample1ClientSecretSyntheticPrivateText));
        Assert.Equal("sample._-1", settings.ClientId);
        Assert.Equal("synthetic-private", settings.ClientSecret);
        Assert.DoesNotContain("synthetic-private", settings.ToString(), StringComparison.Ordinal);
        Assert.ThrowsAny<Exception>(() => SampleSettings.Parse(new byte[OversizedInputBytes]));
    }

    /// <summary>One atomic private file becomes a frozen snapshot, and a second or cancelled setup cannot replace it.</summary>
    [Fact]
    public async Task Given_AbsentConfiguration_When_SetupPublishes_Then_FileIsPrivateAndCannotBeReplaced()
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }
        var directory = Path.Combine(Path.GetTempPath(), TemporaryDirectoryPrefix + Guid.NewGuid().ToString(CompactGuidFormat));
        Directory.CreateDirectory(directory, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        try
        {
            var path = Path.Combine(directory, ConfigurationFilename);
            var source = new SampleConfiguration(path);
            Assert.Null(source.Read());
            using var first = Input(FirstText);
            await SampleConfiguration.ConfigureAsync(first, TestContext.Current.CancellationToken, path);
            Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, File.GetUnixFileMode(path));
            Assert.Equal("first", source.Read()!.ClientId);
            using var duplicate = Input(SecondText);
            await Assert.ThrowsAsync<IOException>(() => SampleConfiguration.ConfigureAsync(duplicate, TestContext.Current.CancellationToken, path));
            using var cancelled = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            await cancelled.CancelAsync();
            using var third = Input(ThirdText);
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => SampleConfiguration.ConfigureAsync(third, cancelled.Token, path));
            Assert.Single(Directory.GetFiles(directory));
            Assert.Equal("first", new SampleConfiguration(path).Read()!.ClientId);
            File.Delete(path);
            Assert.Equal("first", source.Read()!.ClientId);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    /// <summary>Creates a synthetic input stream without process arguments or environment credentials.</summary>
    private static MemoryStream Input(string id) => new(Encoding.UTF8.GetBytes(ClientIdText + id + ClientSecretSyntheticText));
}
