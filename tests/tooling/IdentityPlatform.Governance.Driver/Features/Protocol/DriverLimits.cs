namespace IdentityPlatform.Governance.Driver.Features.Protocol;

/// <summary>Bounds test transport and orchestration independently of the unchanged production operation deadline.</summary>
internal static class DriverLimits
{
    internal const int FrameBytes = 65_536;
    internal const int JsonDepth = 8;
    internal const int SecretCharacters = 8_192;
    internal const int IdentifierCharacters = 450;
    internal const int CodeCharacters = 64;
    internal const int ProcessSeconds = 60;
    internal const int Success = 0;
    internal const int Failure = 2;
    internal const string PostgreSql = "PostgreSql";
    internal const string SqlServer = "SqlServer";
    internal const string Accepted = "Accepted";
    internal const string Denied = "Denied";
    internal const string NoChange = "NoChange";
    internal const string Newline = "\n";
    internal const string Read = "Read";
    internal const string Execute = "Execute";
    internal const string Acknowledgement = "ack";
}
