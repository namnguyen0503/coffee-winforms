using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Tests;

public class InventoryServiceTests
{
    [Fact]
    public async Task GetIngredientsAsync_FiltersLowStockSearchAndOrdersLowStockFirst()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var invSvc = fx.CreateInventoryService();

        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            var milk = await db.Ingredients.SingleAsync(i => i.Name == "Sữa tươi");
            milk.Quantity = milk.MinQuantity; // Boundary: <= MinQuantity counts as low stock
            await db.SaveChangesAsync();
        }

        var lowOnly = await invSvc.GetIngredientsAsync(lowStockOnly: true);
        Assert.Single(lowOnly);
        Assert.Equal("Sữa tươi", lowOnly[0].Name);
        Assert.True(lowOnly[0].IsLowStock);

        var all = await invSvc.GetIngredientsAsync();
        Assert.Equal("Sữa tươi", all[0].Name); // Low stock floats to top

        var search = await invSvc.GetIngredientsAsync("cà phê");
        Assert.Single(search);
    }

    [Fact]
    public async Task AddIngredientAsync_ValidatesRolesDuplicatesAndInitialStock()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var invSvc = fx.CreateInventoryService();

        int adminId, staffId, whId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;
            whId = (await db.Users.SingleAsync(u => u.Username == "thukho")).Id;
        }

        // Validation & permissions
        Assert.False((await invSvc.AddIngredientAsync(new AddIngredientRequest("", "g", 10m, 5m, 1000m), adminId)).Success);
        Assert.False((await invSvc.AddIngredientAsync(new AddIngredientRequest("Mật ong", "", 10m, 5m, 1000m), adminId)).Success);
        Assert.False((await invSvc.AddIngredientAsync(new AddIngredientRequest("Mật ong", "ml", -1m, 5m, 1000m), adminId)).Success);
        Assert.False((await invSvc.AddIngredientAsync(new AddIngredientRequest("Mật ong", "ml", 10m, -1m, 1000m), adminId)).Success);
        Assert.False((await invSvc.AddIngredientAsync(new AddIngredientRequest("Mật ong", "ml", 10m, 5m, -1m), adminId)).Success);
        Assert.False((await invSvc.AddIngredientAsync(new AddIngredientRequest("Mật ong", "ml", 10m, 5m, 1000m), staffId)).Success);
        Assert.False((await invSvc.AddIngredientAsync(new AddIngredientRequest("Mật ong", "ml", 10m, 5m, 1000m), 99999)).Success);
        Assert.False((await invSvc.AddIngredientAsync(new AddIngredientRequest("hạt cà phê", "g", 10m, 5m, 1000m), adminId)).Success);

        // Zero initial quantity -> WAC 0 and no Import transaction created
        var zeroAdd = await invSvc.AddIngredientAsync(new AddIngredientRequest("Siro Vani", "ml", 0m, 100m, 0m), whId);
        Assert.True(zeroAdd.Success);
        Assert.Equal(0m, zeroAdd.Data!.AverageUnitCost);
        Assert.Empty(await invSvc.GetTransactionsAsync(new InventoryLogFilterRequest(IngredientId: zeroAdd.Data.Id)));

        // Nonzero initial quantity -> WAC computed and Import transaction logged
        var nonZeroAdd = await invSvc.AddIngredientAsync(new AddIngredientRequest("Siro Đào", "ml", 1000m, 200m, 250000m), whId);
        Assert.True(nonZeroAdd.Success);
        Assert.Equal(250m, nonZeroAdd.Data!.AverageUnitCost);
        var logs = await invSvc.GetTransactionsAsync(new InventoryLogFilterRequest(IngredientId: nonZeroAdd.Data.Id));
        Assert.Single(logs);
        Assert.Equal(InventoryTransactionType.Import, logs[0].Type);
    }

    [Fact]
    public async Task ImportAndExport_RoleChecksValidationWacAndLogs()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var invSvc = fx.CreateInventoryService();

        int adminId, staffId, whId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;
            whId = (await db.Users.SingleAsync(u => u.Username == "thukho")).Id;
        }

        var sugar = (await invSvc.AddIngredientAsync(new AddIngredientRequest("Đường cát", "g", 1000m, 200m, 20000m), adminId)).Data!; // 20đ/g

        // Import validation & permission
        Assert.False((await invSvc.ImportStockAsync(new ImportStockRequest(0, 100m, 1000m, null), whId)).Success);
        Assert.False((await invSvc.ImportStockAsync(new ImportStockRequest(sugar.Id, 0m, 1000m, null), whId)).Success);
        Assert.False((await invSvc.ImportStockAsync(new ImportStockRequest(sugar.Id, 100m, -1m, null), whId)).Success);
        Assert.False((await invSvc.ImportStockAsync(new ImportStockRequest(sugar.Id, 100m, 1000m, null), staffId)).Success);
        Assert.False((await invSvc.ImportStockAsync(new ImportStockRequest(99999, 100m, 1000m, null), whId)).Success);

        // Import 1000g @ 40,000đ -> total 2000g @ 60,000đ -> 30đ/g
        Assert.True((await invSvc.ImportStockAsync(new ImportStockRequest(sugar.Id, 1000m, 40000m, "NCC A"), whId)).Success);

        // Export validation & permission
        Assert.False((await invSvc.ManualExportAsync(new ManualExportRequest(0, 10m, "Hỏng"), whId)).Success);
        Assert.False((await invSvc.ManualExportAsync(new ManualExportRequest(sugar.Id, 0m, "Hỏng"), whId)).Success);
        Assert.False((await invSvc.ManualExportAsync(new ManualExportRequest(sugar.Id, 10m, "  "), whId)).Success);
        Assert.False((await invSvc.ManualExportAsync(new ManualExportRequest(sugar.Id, 10m, "Hỏng"), staffId)).Success);
        Assert.False((await invSvc.ManualExportAsync(new ManualExportRequest(sugar.Id, 99999m, "Hỏng"), whId)).Success);

        Assert.True((await invSvc.ManualExportAsync(new ManualExportRequest(sugar.Id, 200m, "Vón cục"), adminId)).Success);
        var updatedSugar = (await invSvc.GetIngredientsAsync("Đường cát")).Single();
        Assert.Equal(1800m, updatedSugar.Quantity);
        Assert.Equal(30m, updatedSugar.AverageUnitCost);

        // Transaction filtering & paging
        var today = DateOnly.FromDateTime(DateTime.Today);
        var exportLogs = await invSvc.GetTransactionsAsync(new InventoryLogFilterRequest(today, today, sugar.Id, InventoryTransactionType.ManualExport, adminId, Page: 1, PageSize: 10));
        Assert.Single(exportLogs);
        var paged = await invSvc.GetTransactionsAsync(new InventoryLogFilterRequest(IngredientId: sugar.Id, Page: 2, PageSize: 2));
        Assert.Single(paged); // 3 total txs -> page 2 of size 2 has 1 item
    }

    [Fact]
    public async Task UpdateMinStocksAsync_UpdatesAtomicallyAndRejectsNegatives()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var invSvc = fx.CreateInventoryService();

        var emptyRes = await invSvc.UpdateMinStocksAsync([]);
        Assert.True(emptyRes.Success);
        Assert.Equal(0, emptyRes.Data);

        var items = await invSvc.GetIngredientsAsync();
        var first = items[0];
        var second = items[1];

        // One negative entry rolls back all updates
        var bad = await invSvc.UpdateMinStocksAsync(
        [
            new MinStockUpdateItem(first.Id, 999m),
            new MinStockUpdateItem(second.Id, -10m)
        ]);
        Assert.False(bad.Success);

        var unchanged = (await invSvc.GetIngredientsAsync()).Single(i => i.Id == first.Id);
        Assert.Equal(first.MinQuantity, unchanged.MinQuantity);

        var good = await invSvc.UpdateMinStocksAsync(
        [
            new MinStockUpdateItem(first.Id, 750m),
            new MinStockUpdateItem(second.Id, 320m)
        ]);
        Assert.True(good.Success);
        Assert.Equal(2, good.Data);
    }
}
