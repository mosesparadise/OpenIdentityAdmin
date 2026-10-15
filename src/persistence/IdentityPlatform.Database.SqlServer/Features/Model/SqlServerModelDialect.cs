namespace IdentityPlatform.Database.SqlServer;

/// <summary>Preserves exact existing SqlServer collations and constraint SQL.</summary>
internal sealed class SqlServerModelDialect : IDatabaseModelDialect
{
    private const string Ordinal = "Latin1_General_100_BIN2";
    private const string Digest = "DATALENGTH([Digest]) = 128 AND [Digest] NOT LIKE '%[^0-9A-F]%'";
    private const string Binding = " AND DATALENGTH([Subject]) > 0 AND DATALENGTH([SourceSession]) > 0";
    private const string Payload = "DATALENGTH([ProtectedTicket]) > 0 AND DATALENGTH([ProtectedTicket]) <= 98304";
    private const string MethodLength = "DATALENGTH([AuthenticationMethods]) = 14";
    public string? IdentityOrdinalCollation => Ordinal;
    public string SessionOrdinalCollation => Ordinal;
    public string SessionDigestConstraint => Digest;
    public string SessionBindingConstraint => Digest + Binding;
    public string SessionPayloadConstraint => Payload;
    public string AuthenticationMethodLengthConstraint => MethodLength;

    /// <summary>Formats only trusted code-owned quoted identifiers, preserving all other expression bytes.</summary>
    public string Identifiers(string expression)
    {
        ArgumentNullException.ThrowIfNull(expression);
        var opening = true;
        return new string(expression.Select(character => character != '"' ? character : (opening = !opening) ? ']' : '[').ToArray());
    }
}
