using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Tests;

public class InventoryAndReportTests
{
    [Fact]
    public async Task ImportStock_UpdatesWeightedAverageCost_AndManualExportPreventsNegativeStock()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var invSvc = fx.CreateInventoryService();
        int whId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
            whId = (await db.Users.SingleAsync(u => u.Username == "thukho")).Id;

        var coffee = (await invSvc.GetIngredientsAsync("Hạt Cà phê")).Single();
        // Initial: 5000g @ 220đ/g = 1,100,000đ. Import 5000g @ 1,400,000đ -> total 10,000g / 2,500,000đ = 250đ/g
        var importRes = await invSvc.ImportStockAsync(
            new ImportStockRequest(coffee.Id, 5000m, 1400000m, "Nhập lô giá mới"), whId);
        Assert.True(importRes.Success);

        var updatedCoffee = (await invSvc.GetIngredientsAsync("Hạt Cà phê")).Single();
        Assert.Equal(10000m, updatedCoffee.Quantity);
        Assert.Equal(250m, updatedCoffee.AverageUnitCost);

        // Manual export exceeding 10,000g must fail
        var badExport = await invSvc.ManualExportAsync(new ManualExportRequest(coffee.Id, 20000m, "Hỏng"), whId);
        Assert.False(badExport.Success);

        var goodExport = await invSvc.ManualExportAsync(new ManualExportRequest(coffee.Id, 500m, "Rang hỏng"), whId);
        Assert.True(goodExport.Success);

        updatedCoffee = (await invSvc.GetIngredientsAsync("Hạt Cà phê")).Single();
        Assert.Equal(9500m, updatedCoffee.Quantity);
        Assert.Equal(250m, updatedCoffee.AverageUnitCost);
    }

    [Fact]
    public async Task FinancialReport_UsesSnapshottedOrderUnitCostAndExcludesCancelledOrders()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var shiftSvc = fx.CreateShiftService();
        var menuSvc = fx.CreateMenuService();
        var orderSvc = fx.CreateOrderService();
        var reportSvc = fx.CreateReportService();

        int adminId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;

        await shiftSvc.StartShiftAsync(adminId, 500000m);
        var black = (await menuSvc.GetProductsAsync()).Single(p => p.Name == "Cà phê Đen"); // price 25,000, cost 20*220=4,400

        var o1 = await orderSvc.CheckoutAsync(new CheckoutRequest(
            adminId, PaymentMethod.Cash, null, null, [new CartItemRequest(black.Id, 2)])); // rev 50k, cost 8.8k
        var o2 = await orderSvc.CheckoutAsync(new CheckoutRequest(
            adminId, PaymentMethod.Transfer, null, null, [new CartItemRequest(black.Id, 1)])); // will cancel

        Assert.True(o1.Success && o2.Success);
        await orderSvc.CancelOrderAsync(o2.Data!.Id, adminId, "Test hủy");

        // Change product price AFTER sale -> historical report MUST keep 50,000đ revenue!
        await menuSvc.SaveProductAsync(new SaveProductRequest(
            black.Id,
            black.Name,
            99000m,
            black.CategoryId,
            black.ImagePath,
            true,
            black.Recipes.Select(r => new RecipeLineRequest(r.IngredientId, r.QuantityRequired)).ToList()));

        var today = DateOnly.FromDateTime(DateTime.Today);
        var report = await reportSvc.GetFinancialReportAsync(today, today);

        Assert.Equal(1, report.TotalOrders);
        Assert.Equal(50000m, report.TotalRevenue);
        Assert.Equal(8800m, report.TotalCogs);
        Assert.Equal(41200m, report.GrossProfit);
        Assert.Equal(50000m, report.CashRevenue);
        Assert.Equal(0m, report.TransferRevenue);
    }
}
