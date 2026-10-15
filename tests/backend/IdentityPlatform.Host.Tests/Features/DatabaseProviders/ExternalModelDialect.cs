using IdentityPlatform.Database;
namespace IdentityPlatform.Host.Tests.Features.DatabaseProviders;

/// <summary>Preserves exact existing PostgreSql collations and constraint SQL.</summary>
internal sealed class ExternalModelDialect(string? identityCollation) : IDatabaseModelDialect
{
    private const string Ordinal = "C";
    private const string Digest = "\"Digest\" ~ '^[0-9A-F]{64}$'";
    private const string Binding = " AND octet_length(\"Subject\") > 0 AND octet_length(\"SourceSession\") > 0";
    private const string Payload = "octet_length(\"ProtectedTicket\") > 0 AND octet_length(\"ProtectedTicket\") <= 98304";
    private const string MethodLength = "octet_length(\"AuthenticationMethods\") = 7";
    public string? IdentityOrdinalCollation => identityCollation;
    public string SessionOrdinalCollation => Ordinal;
    public string SessionDigestConstraint => Digest;
    public string SessionBindingConstraint => Digest + Binding;
    public string SessionPayloadConstraint => Payload;
    public string AuthenticationMethodLengthConstraint => MethodLength;

    /// <summary>Formats only trusted code-owned quoted identifiers, preserving all other expression bytes.</summary>
    public string Identifiers(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        return expression;
    }
}
