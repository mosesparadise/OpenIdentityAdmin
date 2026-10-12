namespace IdentityPlatform.AuthServer.Features.Accounts.Shared;

/// <summary>Owns fixed account transport limits shared by pilot and administrator composition.</summary>
internal static class AccountPolicy
{
    internal const string AccountPrefix = "/account";
    internal const string ProtocolPrefix = "/connect";
    internal const string AssetsPrefix = "/assets";
    internal const string Alive = "/alive";
    internal const string Health = "/health";
    internal const string Root = "/";
    internal const string PageRoot = "/Features/Accounts";
    internal const string NonceItem = "CspNonce";
    internal const string AntiforgeryCookie = "__Host-pilot-antiforgery";
    internal const string Partition = "accounts";
    internal const string OperationalPartition = "operational";
    internal const string TextContentType = "text/plain; charset=utf-8";
    internal const string RateDenied = "Too many sign-in requests. Wait a minute and try again.";
    internal const string StatusMessage = "This request cannot be completed. Return to your application and start a new sign-in.";
    internal const int BodyBytes = 16_384;
    internal const int RequestsPerMinute = 600;
    internal const int ConcurrentRequests = 16;
    internal const int RequestSeconds = 10;
    internal const int DatabaseSeconds = 5;
    internal const int NonceBytes = 24;
    internal const int FormValues = 16;
    internal const int FormKeyLength = 128;
    internal const int FormValueLength = 8192;
    internal const int PasswordLength = 16;
    internal const int PasswordIterations = 210_000;
    internal const int FailedAttempts = 5;
    internal const int LockoutMinutes = 5;
}
