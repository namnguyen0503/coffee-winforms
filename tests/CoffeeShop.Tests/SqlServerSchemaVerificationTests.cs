using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;
using CoffeeShop.Infrastructure.Data;
using CoffeeShop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Tests;

public class SqlServerSchemaVerificationTests
{
    [Fact]
    public void SqlServerModel_GeneratesValidCreateScript_WithoutMultipleCascadePaths()
    {
        var options = new DbContextOptionsBuilder<CoffeeDbContext>()
            .UseSqlServer("Server=.;Database=CoffeeShopDb;Trusted_Connection=True;TrustServerCertificate=True;")
            .Options;

        using var db = new CoffeeDbContext(options);
        var ddl = db.Database.GenerateCreateScript();

        Assert.False(string.IsNullOrWhiteSpace(ddl));

        string[] expectedTables =
        [
            "CREATE TABLE [Categories]",
            "CREATE TABLE [Products]",
            "CREATE TABLE [Ingredients]",
            "CREATE TABLE [RecipeItems]",
            "CREATE TABLE [Users]",
            "CREATE TABLE [WorkSchedules]",
            "CREATE TABLE [WorkSessions]",
            "CREATE TABLE [Vouchers]",
            "CREATE TABLE [Orders]",
            "CREATE TABLE [OrderItems]",
            "CREATE TABLE [InventoryTransactions]",
            "CREATE TABLE [CashTransactions]"
        ];

        foreach (var tableSql in expectedTables)
            Assert.Contains(tableSql, ddl);
    }

    [Fact]
    public async Task SqlServer_LiveEndToEnd_CreateSeedCheckoutCancelAndReport()
    {
        var dbName = $"CoffeeShopDb_Test_{Guid.NewGuid():N}";
        var connStr = $"Server=.;Database={dbName};Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True;Connect Timeout=15";
        var options = new DbContextOptionsBuilder<CoffeeDbContext>()
            .UseSqlServer(connStr)
            .Options;

        IDbContextFactory<CoffeeDbContext> factory = new SqlServerDbContextFactory(options);

        try
        {
            await using (var db = await factory.CreateDbContextAsync())
            {
                await db.Database.EnsureCreatedAsync();
                await DbSeeder.SeedAsync(db);
                await DbSeeder.SeedAsync(db); // idempotent
            }

            var settings = new AppSettings { EnforceStaffSchedule = true };
            var authSvc = new AuthService(factory, settings);
            var shiftSvc = new ShiftService(factory);
            var menuSvc = new MenuService(factory);
            var orderSvc = new OrderService(factory);
            var invSvc = new InventoryService(factory);
            var reportSvc = new ReportService(factory);

            var adminLogin = await authSvc.LoginAsync("admin", "123456", DateTime.Today.AddHours(10));
            Assert.True(adminLogin.Success);
            var adminId = adminLogin.User!.Id;

            var startRes = await shiftSvc.StartShiftAsync(adminId, 500000m);
            Assert.True(startRes.Success);

            var latte = (await menuSvc.GetProductsAsync()).Single(p => p.Name == "Latte Đá");
            var order1 = await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "WELCOME", null, [new CartItemRequest(latte.Id, 2)]));
            var order2 = await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Transfer, null, null, [new CartItemRequest(latte.Id, 1)]));
            Assert.True(order1.Success && order2.Success);

            Assert.True((await orderSvc.CancelOrderAsync(order2.Data!.Id, adminId, "SQL Server integration cancel")).Success);

            var coffeeBean = (await invSvc.GetIngredientsAsync("Hạt Cà phê")).Single();
            Assert.True((await invSvc.ImportStockAsync(new ImportStockRequest(coffeeBean.Id, 1000m, 300000m, "SQL test"), adminId)).Success);

            // Verify schema upgrader is idempotent on an existing SQL Server database
            await using (var upgradeDb = await factory.CreateDbContextAsync())
            {
                await DbSchemaUpgrader.EnsureLatestSchemaAsync(upgradeDb);
            }

            var today = DateOnly.FromDateTime(DateTime.Today);
            var report = await reportSvc.GetFinancialReportAsync(today, today);
            Assert.Equal(1, report.TotalOrders);
            Assert.Equal(81000m, report.TotalRevenue);

            var withdrawRes = await shiftSvc.WithdrawCashAsync(adminId, new AdminWithdrawCashRequest(31000m, "SQL Server Admin rút két"));
            Assert.True(withdrawRes.Success);

            var endRes = await shiftSvc.EndShiftAsync(adminId, 550000m, "SQL Server E2E");
            Assert.True(endRes.Success);
            Assert.Equal(0m, endRes.Data!.CashVariance);

            var cashFlow = await shiftSvc.GetCashFlowAsync(today, today);
            Assert.Single(cashFlow);
            Assert.Equal(31000m, cashFlow[0].CashWithdrawn);

            var cashLogs = await shiftSvc.GetCashTransactionsAsync(today, today);
            Assert.Single(cashLogs);
            Assert.Equal(31000m, cashLogs[0].Amount);
            Assert.Equal("SQL Server Admin rút két", cashLogs[0].Reason);
        }
        finally
        {
            await using var cleanupDb = await factory.CreateDbContextAsync();
            await cleanupDb.Database.EnsureDeletedAsync();
        }
    }

    private sealed class SqlServerDbContextFactory : IDbContextFactory<CoffeeDbContext>
    {
        private readonly DbContextOptions<CoffeeDbContext> _options;
        public SqlServerDbContextFactory(DbContextOptions<CoffeeDbContext> options) => _options = options;
        public CoffeeDbContext CreateDbContext() => new(_options);
    }
}
