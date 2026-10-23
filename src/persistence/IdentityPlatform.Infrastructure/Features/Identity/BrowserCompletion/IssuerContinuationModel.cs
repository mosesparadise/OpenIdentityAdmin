using IdentityPlatform.Database;
using Microsoft.EntityFrameworkCore;

namespace IdentityPlatform.Infrastructure.Features.Identity.BrowserCompletion;

/// <summary>Maps bounded ordinal snapshots without cascades that could erase completion replay markers during account cleanup.</summary>
internal static class IssuerContinuationModel
{
    private const string Table = "AdministratorIssuerContinuation";
    private const string RevisionConstraint = "CK_AdministratorIssuerContinuation_Revision";
    private const string TimeConstraint = "CK_AdministratorIssuerContinuation_Time";
    private const string RevisionSql = "\"Revision\" > 0";
    private const string TimeSql = "\"CreatedAt\" > 0 AND \"ExpiresAt\" > \"CreatedAt\" AND \"RetainUntil\" >= \"ExpiresAt\"";

    /// <summary>Composes portable constraints with the selected provider's existing identifier dialect.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var row = builder.Entity<IssuerContinuation>();
        row.HasKey(value => value.IntentDigest);
        row.Property(value => value.IntentDigest).HasMaxLength(IssuerContinuationPolicy.DigestLength);
        row.Property(value => value.BrowserDigest).HasMaxLength(IssuerContinuationPolicy.DigestLength);
        row.Property(value => value.CompletionDigest).HasMaxLength(IssuerContinuationPolicy.DigestLength);
        row.Property(value => value.InteractionBrowserDigest).HasMaxLength(IssuerContinuationPolicy.DigestLength);
        row.Property(value => value.BrowserBudgetId).HasMaxLength(IssuerContinuationPolicy.DigestLength);
        row.Property(value => value.State).HasMaxLength(IssuerContinuationPolicy.StateLength);
        foreach (var name in new[] { nameof(IssuerContinuation.ClientId), nameof(IssuerContinuation.InteractionId),
            nameof(IssuerContinuation.UserId), nameof(IssuerContinuation.SessionId), nameof(IssuerContinuation.RequestId) })
        {
            row.Property<string>(name).HasMaxLength(IssuerContinuationPolicy.ReferenceLength);
        }
        row.HasIndex(value => value.BrowserDigest);
        row.HasIndex(value => value.InteractionId).IsUnique();
        row.HasIndex(value => value.CompletionDigest).IsUnique();
        row.HasIndex(value => value.RequestId).IsUnique();
        row.HasIndex(value => value.RetainUntil);
        row.Property(value => value.Revision).IsConcurrencyToken();
        row.ToTable(Table, table =>
        {
            table.HasCheckConstraint(RevisionConstraint, dialect.Identifiers(RevisionSql));
            table.HasCheckConstraint(TimeConstraint, dialect.Identifiers(TimeSql));
        });
        if (dialect.IdentityOrdinalCollation is { } ordinal)
        {
            foreach (var property in row.Metadata.GetProperties().Where(property => property.ClrType == typeof(string)))
            {
                property.SetCollation(ordinal);
            }
        }
    }
}
