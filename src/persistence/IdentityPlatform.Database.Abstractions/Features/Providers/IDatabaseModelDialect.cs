namespace IdentityPlatform.Database;

/// <summary>Describes exact storage expressions needed by the existing product model; inputs are trusted code only.</summary>
/// <example><code>property.UseCollation(provider.Model.SessionOrdinalCollation);</code></example>
public interface IDatabaseModelDialect
{
    string? IdentityOrdinalCollation
    {
        get;
    }
    string SessionOrdinalCollation
    {
        get;
    }
    string SessionDigestConstraint
    {
        get;
    }
    string SessionBindingConstraint
    {
        get;
    }
    string SessionPayloadConstraint
    {
        get;
    }
    string AuthenticationMethodLengthConstraint
    {
        get;
    }

    /// <summary>Translates double-quoted code-owned column identifiers; never accepts user SQL.</summary>
    /// <example><code>var sql = provider.Model.Identifiers("\"Revision\" &gt;= 0");</code></example>
    string Identifiers(string expression);
}
