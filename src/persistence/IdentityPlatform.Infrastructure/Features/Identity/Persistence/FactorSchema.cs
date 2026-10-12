using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityPlatform.Infrastructure.Features.Identity.Persistence;

/// <summary>Applies consistent provider-portable storage bounds without widening native Identity identifiers.</summary>
internal static class FactorSchema
{
    internal const int UserLength = 450;
    internal const int DigestLength = 64;
    internal const int LabelLength = 32;
    internal const int StampLength = 256;
    internal const int CiphertextLength = 2048;
    private const string Ordinal = "Latin1_General_100_BIN2";
    private const string ConstraintPrefix = "CK_";
    private const string Separator = "_";

    /// <summary>Bounds a text column and keeps SQL Server comparisons ordinal.</summary>
    internal static void Text<T>(EntityTypeBuilder<T> entity, string name, int length, bool sqlServer) where T : class
    {
        var property = entity.Property<string>(name).HasMaxLength(length);
        if (sqlServer)
        {
            property.UseCollation(Ordinal);
        }
    }

    /// <summary>Associates product material with durable user authentication state without destructive cascades.</summary>
    internal static EntityTypeBuilder<T> Material<T>(ModelBuilder builder, bool sqlServer) where T : class
    {
        var entity = builder.Entity<T>();
        entity.Property<Guid>(nameof(AdministratorFactor.Id)).ValueGeneratedNever();
        Text(entity, nameof(AdministratorFactor.UserId), UserLength, sqlServer);
        entity.HasOne<AdministratorAuthentication>().WithMany().HasForeignKey(nameof(AdministratorFactor.UserId)).OnDelete(DeleteBehavior.Restrict);
        return entity;
    }

    /// <summary>Uses code-owned expressions and stable constraint names for both provider migrations.</summary>
    internal static void Check<T>(EntityTypeBuilder<T> entity, string rule, string expression, bool sqlServer) where T : class
    {
        var table = typeof(T).Name;
        var sql = sqlServer ? Brackets(expression) : expression;
        entity.ToTable(table, mapping => mapping.HasCheckConstraint(ConstraintPrefix + table + Separator + rule, sql));
    }

    /// <summary>Translates only quoted, code-owned column identifiers to SQL Server delimiters.</summary>
    private static string Brackets(string expression)
    {
        var opening = true;
        return new string(expression.Select(character => character != '"' ? character : (opening = !opening) ? ']' : '[').ToArray());
    }
}
