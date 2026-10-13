namespace IdentityPlatform.Administrator.Tests.Fixtures.Governance;

/// <summary>Names the exact reviewed proof-boundary rows independently of runtime policy dispatch.</summary>
internal static class ProofCases
{
    internal const string Aged = "age-300-seconds";
    internal const string Future = "future-time";
    internal const string Consumed = "consumed-proof";
    internal const string Password = "wrong-password";
    internal const string Totp = "wrong-totp";
    internal const string Session = "different-session";
    internal const string Target = "different-target";
    internal const string Action = "different-action";
    internal const string Payload = "changed-payload";
    internal const string Version = "changed-expected-version";
    internal const string Foreign = "foreign-live-context";
    internal const string Ended = "original-transaction-ended";
    internal const string Rehash = "native-password-rehash";
    internal static readonly string[] Invalid = [Aged, Future, Consumed, Password, Totp, Session, Target, Action, Payload, Version];
}
