namespace IdentityPlatform.Issuer.Lifecycle.Tests.Features.Distribution;

/// <summary>Defines a synthetic lock-only native-row conflict for exercising the SQL observation alternative.</summary>
internal static class PilotReplayNativeReadSql
{
    internal const string Provider = "SqlServer";
    internal const string CodeParameter = "code";
    internal const string Create = "CREATE TABLE [platform].[PilotReplayTestNativeReadBarrier] ([Id] int PRIMARY KEY, [Touch] int NOT NULL); INSERT INTO [platform].[PilotReplayTestNativeReadBarrier] VALUES (1,0); ALTER TABLE [platform].[PilotReplayTestTarget] ADD [CodeId] nvarchar(450) NULL";
    internal const string Target = "UPDATE [platform].[PilotReplayTestTarget] SET [CodeId]=@code";
    internal const string Hold = "UPDATE [platform].[PilotReplayTestNativeReadBarrier] WITH (ROWLOCK) SET [Touch]=[Touch] WHERE [Id]=1";
    internal const string Drop = "DROP TABLE IF EXISTS [platform].[PilotReplayTestNativeReadBarrier]";
    internal const string Trigger = """
        ALTER TRIGGER [platform].[PilotReplayTestInsert] ON [platform].[OpenIddictTokens]
        WITH EXECUTE AS OWNER AFTER INSERT AS
        BEGIN
          SET NOCOUNT ON;
          IF EXISTS (SELECT 1 FROM inserted i JOIN [platform].[PilotReplayTestTarget] t
            ON t.[AuthorizationId]=i.[AuthorizationId] WHERE i.[Type]='urn:ietf:params:oauth:token-type:access_token')
          BEGIN
            UPDATE [platform].[PilotReplayTestBarrier] WITH (ROWLOCK) SET [Touch]=[Touch] WHERE [Id]=1;
            DECLARE @lockedCode nvarchar(450);
            SELECT @lockedCode=c.[Id] FROM [platform].[OpenIddictTokens] c WITH (XLOCK,ROWLOCK,HOLDLOCK)
              JOIN [platform].[PilotReplayTestTarget] t ON t.[CodeId]=c.[Id] AND t.[AuthorizationId]=c.[AuthorizationId]
              WHERE c.[Status]='redeemed';
            IF @@ROWCOUNT<>1 THROW 51001, 'Synthetic native-read barrier requires one consumed code.', 1;
            UPDATE [platform].[PilotReplayTestNativeReadBarrier] WITH (ROWLOCK) SET [Touch]=[Touch] WHERE [Id]=1;
          END
        END
        """;
}
