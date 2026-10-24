using IdentityPlatform.Database;
using IdentityPlatform.Infrastructure.Features.Sessions.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace IdentityPlatform.Infrastructure.Features.Sessions.Browser;

/// <summary>Maps additive bounded coordination records using the registered provider's existing portable dialect.</summary>
internal static class BrowserSessionModel
{
    private const string ContextRule = "\"Generation\" > 0 AND \"Revision\" > 0 AND \"ObservedAt\" > 0 AND \"ExpiresAt\" > 0 AND \"RetainUntil\" >= \"ExpiresAt\"";
    private const string OperationRule = "\"Generation\" > 0 AND \"Revision\" > 0 AND \"CreatedAt\" > 0 AND \"ObservedAt\" >= \"CreatedAt\" AND \"ExpiresAt\" > \"CreatedAt\" AND \"ExpiresAt\" - \"CreatedAt\" <= 3000000000 AND \"RetainUntil\" >= \"ExpiresAt\"";

    private const string BudgetConstraint = "CK_BrowserSessionBudget_State";
    private const string ContextConstraint = "CK_BrowserContext_State";
    private const string IntentConstraint = "CK_BrowserLoginIntent_State";
    private const string LogoutConstraint = "CK_BrowserLogoutContinuation_State";
    private const string AuditConstraint = "CK_BrowserSessionAudit_State";
    private const string ConstraintPrefix = "CK_";
    private const string LifetimeSuffix = "_Lifetime";
    private const string BudgetRule = "\"Id\" = 1 AND \"Revision\" >= 0";
    private const string IntentStateRule = "\"State\" IN (1, 2, 3, 4, 5)";
    private const string LogoutStateRule = "\"State\" IN (1, 2, 3, 4)";
    private const string AuditRule = "\"Generation\" > 0 AND \"OccurredAt\" > 0 AND \"Operation\" IN (1, 2, 3, 4, 5, 6, 7) AND ((\"Subject\" IS NULL AND \"SourceSession\" IS NULL) OR (\"Subject\" IS NOT NULL AND \"SourceSession\" IS NOT NULL))";

    /// <summary>Seeds only inert admission ordering; no browser context or authenticated identity is created by migration.</summary>
    internal static void Configure(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var budget = builder.Entity<BrowserSessionBudget>();
        budget.Property(row => row.Id).ValueGeneratedNever();
        budget.HasData(new BrowserSessionBudget());
        budget.ToTable(nameof(BrowserSessionBudget), table => table.HasCheckConstraint(BudgetConstraint,
            dialect.Identifiers(BudgetRule)));
        var context = builder.Entity<BrowserContext>();
        context.HasKey(row => row.Digest);
        context.Property(row => row.Digest).HasMaxLength(BrowserSessionPolicy.DigestLength);
        context.Property(row => row.TicketDigest).HasMaxLength(BrowserSessionPolicy.DigestLength);
        context.Property(row => row.Revision).IsConcurrencyToken();
        context.HasIndex(row => row.RetainUntil);
        context.ToTable(nameof(BrowserContext), table => table.HasCheckConstraint(ContextConstraint, dialect.Identifiers(ContextRule)));
        ConfigureIntent(builder, dialect);
        ConfigureLogout(builder, dialect);
        ConfigureAudit(builder, dialect);
        foreach (var type in new[] { typeof(BrowserContext), typeof(BrowserLoginIntent), typeof(BrowserLogoutContinuation), typeof(BrowserSessionAudit) })
        {
            foreach (var property in builder.Entity(type).Metadata.GetProperties().Where(property => property.ClrType == typeof(string)))
            {
                property.SetCollation(dialect.SessionOrdinalCollation);
            }
        }
    }

    /// <summary>Bounds the fixed client and native reservation without a foreign key that would interfere with native cleanup.</summary>
    private static void ConfigureIntent(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var intent = builder.Entity<BrowserLoginIntent>();
        Operation(intent, dialect, nameof(BrowserLoginIntent));
        intent.Property(row => row.ClientId).HasMaxLength(SessionTicketLimits.BindingLength);
        intent.Property(row => row.NativeStateDigest).HasMaxLength(BrowserSessionPolicy.DigestLength);
        intent.Property(row => row.TicketDigest).HasMaxLength(BrowserSessionPolicy.DigestLength);
        intent.HasIndex(row => row.NativeStateDigest).IsUnique();
        intent.HasOne<BrowserContext>().WithMany().HasForeignKey(row => row.BrowserDigest).OnDelete(DeleteBehavior.Restrict);
        intent.ToTable(table => table.HasCheckConstraint(IntentConstraint, dialect.Identifiers(IntentStateRule)));
    }

    /// <summary>Keeps source selectors server-side and makes returned-unverified the only successful return state.</summary>
    private static void ConfigureLogout(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var logout = builder.Entity<BrowserLogoutContinuation>();
        Operation(logout, dialect, nameof(BrowserLogoutContinuation));
        logout.Property(row => row.Subject).HasMaxLength(SessionTicketLimits.BindingLength);
        logout.Property(row => row.SourceSession).HasMaxLength(SessionTicketLimits.BindingLength);
        logout.HasOne<BrowserContext>().WithMany().HasForeignKey(row => row.BrowserDigest).OnDelete(DeleteBehavior.Restrict);
        logout.ToTable(table => table.HasCheckConstraint(LogoutConstraint, dialect.Identifiers(LogoutStateRule)));
    }

    /// <summary>Applies identical lifetime, key and index semantics to both finite operation categories.</summary>
    private static void Operation<T>(EntityTypeBuilder<T> row, IDatabaseModelDialect dialect, string table) where T : class
    {
        row.HasKey(nameof(BrowserLoginIntent.Digest));
        row.Property<string>(nameof(BrowserLoginIntent.Digest)).HasMaxLength(BrowserSessionPolicy.DigestLength);
        row.Property<string>(nameof(BrowserLoginIntent.BrowserDigest)).HasMaxLength(BrowserSessionPolicy.DigestLength);
        row.Property<long>(nameof(BrowserLoginIntent.Revision)).IsConcurrencyToken();
        row.HasIndex(nameof(BrowserLoginIntent.BrowserDigest), nameof(BrowserLoginIntent.Generation));
        row.HasIndex(nameof(BrowserLoginIntent.ExpiresAt), nameof(BrowserLoginIntent.State));
        row.HasIndex(nameof(BrowserLoginIntent.RetainUntil));
        row.ToTable(table, mapped => mapped.HasCheckConstraint(ConstraintPrefix + table + LifetimeSuffix, dialect.Identifiers(OperationRule)));
    }

    /// <summary>Retains audit independently of child cleanup, with either complete actor binding or explicit anonymity.</summary>
    private static void ConfigureAudit(ModelBuilder builder, IDatabaseModelDialect dialect)
    {
        var audit = builder.Entity<BrowserSessionAudit>();
        audit.Property(row => row.Id).ValueGeneratedNever();
        audit.Property(row => row.Subject).HasMaxLength(SessionTicketLimits.BindingLength);
        audit.Property(row => row.SourceSession).HasMaxLength(SessionTicketLimits.BindingLength);
        audit.HasIndex(row => new { row.CorrelationId, row.Generation });
        audit.ToTable(nameof(BrowserSessionAudit), table => table.HasCheckConstraint(AuditConstraint,
            dialect.Identifiers(AuditRule)));
    }
}
