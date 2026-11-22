using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Data;

public static class DbSchemaUpgrader
{
    public static async Task EnsureLatestSchemaAsync(CoffeeDbContext db)
    {
        await db.Database.EnsureCreatedAsync();

        if (!db.Database.IsSqlServer())
            return;

        const string upgradeSql = """
            IF OBJECT_ID(N'[dbo].[CashTransactions]', N'U') IS NULL
            BEGIN
                CREATE TABLE [dbo].[CashTransactions] (
                    [Id] int NOT NULL IDENTITY,
                    [WorkSessionId] int NULL,
                    [UserId] int NOT NULL,
                    [Type] int NOT NULL,
                    [Amount] decimal(18,2) NOT NULL,
                    [Reason] nvarchar(500) NOT NULL,
                    [CreatedAt] datetime2 NOT NULL,
                    CONSTRAINT [PK_CashTransactions] PRIMARY KEY ([Id]),
                    CONSTRAINT [FK_CashTransactions_Users_UserId] FOREIGN KEY ([UserId]) REFERENCES [dbo].[Users] ([Id]) ON DELETE NO ACTION,
                    CONSTRAINT [FK_CashTransactions_WorkSessions_WorkSessionId] FOREIGN KEY ([WorkSessionId]) REFERENCES [dbo].[WorkSessions] ([Id]) ON DELETE NO ACTION
                );
                CREATE INDEX [IX_CashTransactions_CreatedAt] ON [dbo].[CashTransactions] ([CreatedAt]);
                CREATE INDEX [IX_CashTransactions_WorkSessionId] ON [dbo].[CashTransactions] ([WorkSessionId]);
            END;
            """;

        await db.Database.ExecuteSqlRawAsync(upgradeSql);
    }
}
