namespace IdentityPlatform.Host.Tests.Features.Samples;

/// <summary>Runs cold native sample hosts without competing test collections; each case retains its real request budget and internal races.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SampleNativeIsolation
{
    public const string Name = "sample-native-host";
}
