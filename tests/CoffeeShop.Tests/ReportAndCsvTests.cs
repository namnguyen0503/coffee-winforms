using System.Globalization;
using System.Text;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;
using CoffeeShop.Infrastructure.Data;
using CoffeeShop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Tests;

public class ReportAndCsvTests
{
    [Fact]
    public async Task DbSeeder_IsIdempotent_AndEmptyDashboardReturnsZeros()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeEmptyAsync();

        var reportSvc = fx.CreateReportService();
        var emptyDash = await reportSvc.GetDashboardSummaryAsync();
        Assert.Equal(0, emptyDash.TodayOrderCount);
        Assert.Equal(0m, emptyDash.TodayRevenue);
        Assert.Equal(0, emptyDash.LowStockCount);
        Assert.Empty(emptyDash.RecentOrders);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var emptyRep = await reportSvc.GetFinancialReportAsync(today, today);
        Assert.Equal(0, emptyRep.TotalOrders);
        Assert.Equal(0m, emptyRep.GrossMarginPercent);

        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            await DbSeeder.SeedAsync(db);
            var firstUsers = await db.Users.CountAsync();
            var firstProducts = await db.Products.CountAsync();
            await DbSeeder.SeedAsync(db); // Second run should be no-op
            Assert.Equal(firstUsers, await db.Users.CountAsync());
            Assert.Equal(firstProducts, await db.Products.CountAsync());
        }
    }

    [Fact]
    public async Task FinancialReport_ProportionallyDistributesVoucherDiscount_AndMatchesTotalsAcrossViews()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var shiftSvc = fx.CreateShiftService();
        var menuSvc = fx.CreateMenuService();
        var orderSvc = fx.CreateOrderService();
        var reportSvc = fx.CreateReportService();
        var invSvc = fx.CreateInventoryService();

        int adminId, staffId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;
        }

        var products = await menuSvc.GetProductsAsync();
        var coffee = products.Single(p => p.Name == "Cà phê Đen"); // Price 25,000, Cost 4,400
        var latte = products.Single(p => p.Name == "Latte Đá"); // Price 45,000, Cost 8,900

        // Staff sells 1 Latte (45k) Cash + Admin sells 1 Coffee + 1 Latte with WELCOME 10% (70k -> 63k) Transfer
        await shiftSvc.StartShiftAsync(staffId, 100000m);
        await orderSvc.CheckoutAsync(new CheckoutRequest(staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(latte.Id, 1)]));

        await shiftSvc.StartShiftAsync(adminId, 200000m);
        await orderSvc.CheckoutAsync(new CheckoutRequest(
            adminId,
            PaymentMethod.Transfer,
            "WELCOME",
            null,
            [new CartItemRequest(coffee.Id, 1), new CartItemRequest(latte.Id, 1)]));

        // Increase coffee ingredient cost after sales -> report COGS must preserve snapshotted 4,400 / 8,900
        var beans = (await invSvc.GetIngredientsAsync("Hạt Cà phê")).Single();
        await invSvc.ImportStockAsync(new ImportStockRequest(beans.Id, 5000m, 5000000m, "Tăng giá"), adminId);

        // Staff-scoped dashboard sees only staff's 45,000; unscoped dashboard sees 108,000
        var staffDash = await reportSvc.GetDashboardSummaryAsync(staffId);
        Assert.Equal(1, staffDash.TodayOrderCount);
        Assert.Equal(45000m, staffDash.TodayRevenue);

        var allDash = await reportSvc.GetDashboardSummaryAsync(null);
        Assert.Equal(2, allDash.TodayOrderCount);
        Assert.Equal(108000m, allDash.TodayRevenue);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var report = await reportSvc.GetFinancialReportAsync(today, today);
        Assert.Equal(2, report.TotalOrders);
        Assert.Equal(108000m, report.TotalRevenue);
        Assert.Equal(45000m, report.CashRevenue);
        Assert.Equal(63000m, report.TransferRevenue);
        // Expected COGS = Latte (8900) + Coffee (4400) + Latte (8900) = 22,200
        Assert.Equal(22200m, report.TotalCogs);
        Assert.Equal(85800m, report.GrossProfit);
        Assert.Equal(report.TotalRevenue, report.TopProducts.Sum(p => p.Revenue));
    }

    [Fact]
    public void CsvExportService_ExportsAllTypesWithUtf8BomEscapingAndInvariantDecimalsUnderViVnCulture()
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "CoffeeCsvTest_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
        var originalCulture = Thread.CurrentThread.CurrentCulture;
        var originalUiCulture = Thread.CurrentThread.CurrentUICulture;

        try
        {
            Thread.CurrentThread.CurrentCulture = CultureInfo.GetCultureInfo("vi-VN");
            Thread.CurrentThread.CurrentUICulture = CultureInfo.GetCultureInfo("vi-VN");

            var svc = new CsvExportService();
            var now = new DateTime(2026, 11, 22, 13, 30, 0);

            var ordersFile = Path.Combine(tempDir, "orders.csv");
            svc.ExportOrders(ordersFile,
            [
                new OrderDto(1, "ORD-1", 1, "Nguyễn \"Admin\", V2", 1, now, 50000.5m, "VIP20", 20m, 10000.1m, 40000.4m,
                    PaymentMethod.Cash, OrderStatus.Paid, null, null, null, [])
            ]);
            var ordersBytes = File.ReadAllBytes(ordersFile);
            Assert.Equal(0xEF, ordersBytes[0]);
            Assert.Equal(0xBB, ordersBytes[1]);
            Assert.Equal(0xBF, ordersBytes[2]);
            var ordersText = File.ReadAllText(ordersFile, Encoding.UTF8);
            Assert.Contains("\"Nguyễn \"\"Admin\"\", V2\"", ordersText);
            Assert.Contains("50000.5", ordersText);

            var finFile = Path.Combine(tempDir, "fin.csv");
            svc.ExportFinancialReport(finFile, new FinancialReportDto(
                100000.5m, 25000.25m, 75000.25m, 75m, 1, 100000.5m, 0m,
                [new OrderProfitRowDto(1, "ORD-1", now, "Admin", "Tiền mặt", 100000.5m, 25000.25m, 75000.25m)],
                []));
            var finText = File.ReadAllText(finFile, Encoding.UTF8);
            Assert.Contains("TONG DOANH THU,,,,100000.5,25000.25,75000.25", finText);

            var invFile = Path.Combine(tempDir, "inv.csv");
            svc.ExportInventoryLogs(invFile,
            [
                new InventoryTransactionDto(1, 1, "Cam tươi", "quả", InventoryTransactionType.SaleExport, 1.5m, 7500m, 5000m, "Note", 1, "Staff", now)
            ]);
            var invText = File.ReadAllText(invFile, Encoding.UTF8);
            Assert.Contains(",1.5,5000,7500,", invText);

            var cfFile = Path.Combine(tempDir, "cf.csv");
            svc.ExportCashFlow(cfFile,
            [
                new CashFlowRowDto(1, "Staff", now, null, WorkSessionStatus.Open, 500000m, 500000m, 0m, 10000m, 0m, 10000m, 490000m, 0m, 0m, null, 20000m)
            ]);
            var cfText = File.ReadAllText(cfFile, Encoding.UTF8);
            Assert.Contains("AdminRutKet", cfText);
            Assert.Contains(",20000,490000,", cfText);
            Assert.Contains("\"Đang mở\"", cfText);
        }
        finally
        {
            Thread.CurrentThread.CurrentCulture = originalCulture;
            Thread.CurrentThread.CurrentUICulture = originalUiCulture;
            if (Directory.Exists(tempDir))
                Directory.Delete(tempDir, recursive: true);
        }
    }
}
