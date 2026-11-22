using CoffeeShop.Application.DTOs;

namespace CoffeeShop.Tests;

public class MenuServiceTests
{
    [Fact]
    public async Task CategoryCrud_ValidatesEmptyDuplicatesAndMissingIds()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var menuSvc = fx.CreateMenuService();

        var initial = await menuSvc.GetCategoriesAsync();
        Assert.Equal(2, initial.Count);

        Assert.False((await menuSvc.SaveCategoryAsync(null, "   ", 3)).Success);
        Assert.False((await menuSvc.SaveCategoryAsync(null, "đồ uống", 3)).Success); // case-insensitive dup
        Assert.False((await menuSvc.SaveCategoryAsync(99999, "Sinh tố", 3)).Success);

        var added = await menuSvc.SaveCategoryAsync(null, "  Trà sữa  ", 3);
        Assert.True(added.Success);
        Assert.Equal("Trà sữa", added.Data!.Name);

        var updated = await menuSvc.SaveCategoryAsync(added.Data.Id, "Trà sữa & Topping", 4);
        Assert.True(updated.Success);
        Assert.Equal("Trà sữa & Topping", updated.Data!.Name);
        Assert.Equal(4, updated.Data.SortOrder);
    }

    [Fact]
    public async Task ProductCrud_NoRecipe_WithRecipeMerging_UpdateAndSoftDelete()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var menuSvc = fx.CreateMenuService();
        var invSvc = fx.CreateInventoryService();

        var catId = (await menuSvc.GetCategoriesAsync()).First().Id;
        var ingredients = await invSvc.GetIngredientsAsync();
        var coffee = ingredients.Single(i => i.Name == "Hạt Cà phê");
        var milk = ingredients.Single(i => i.Name == "Sữa tươi");

        // Validation failures
        Assert.False((await menuSvc.SaveProductAsync(new SaveProductRequest(null, "  ", 20000m, catId, null, true, []))).Success);
        Assert.False((await menuSvc.SaveProductAsync(new SaveProductRequest(null, "Test", -100m, catId, null, true, []))).Success);
        Assert.False((await menuSvc.SaveProductAsync(new SaveProductRequest(null, "Test", 20000m, 99999, null, true, []))).Success);
        Assert.False((await menuSvc.SaveProductAsync(new SaveProductRequest(null, "Test", 20000m, catId, null, true, [new RecipeLineRequest(99999, 10m)]))).Success);
        Assert.False((await menuSvc.SaveProductAsync(new SaveProductRequest(99999, "Test", 20000m, catId, null, true, []))).Success);

        // Product without recipe
        var noRecipe = await menuSvc.SaveProductAsync(new SaveProductRequest(
            null, "Nước Suối", 10000m, catId, null, true, []));
        Assert.True(noRecipe.Success);
        Assert.Null(noRecipe.Data!.MaxProducibleQuantity);
        Assert.Equal(0m, noRecipe.Data.EstimatedUnitCost);

        // Product with duplicate recipe lines for the same ingredient -> merged cleanly
        var customDrink = await menuSvc.SaveProductAsync(new SaveProductRequest(
            null,
            "Bạc Xỉu Đặc Biệt",
            38000m,
            catId,
            "Assets/Images/drink/latte-da.jpg",
            IsAvailable: true,
            Recipes:
            [
                new RecipeLineRequest(coffee.Id, 10m),
                new RecipeLineRequest(coffee.Id, 5m), // merged to 15g
                new RecipeLineRequest(milk.Id, 100m)
            ]));
        Assert.True(customDrink.Success);
        Assert.Equal(2, customDrink.Data!.Recipes.Count);
        Assert.Equal(15m, customDrink.Data.Recipes.Single(r => r.IngredientId == coffee.Id).QuantityRequired);
        // Cost: 15 * 220 + 100 * 30 = 3300 + 3000 = 6300
        Assert.Equal(6300m, customDrink.Data.EstimatedUnitCost);

        // Update product to unavailable & replace recipe
        var updated = await menuSvc.SaveProductAsync(new SaveProductRequest(
            customDrink.Data.Id,
            "Bạc Xỉu Tạm Hết",
            40000m,
            catId,
            null, // preserves existing ImagePath
            IsAvailable: false,
            Recipes: [new RecipeLineRequest(milk.Id, 200m)]));
        Assert.True(updated.Success);
        Assert.Single(updated.Data!.Recipes);
        Assert.Equal("Assets/Images/drink/latte-da.jpg", updated.Data.ImagePath);
        Assert.False(updated.Data.IsAvailable);

        // Filtering includeUnavailable = false excludes it
        var activeOnly = await menuSvc.GetProductsAsync(catId, "Bạc Xỉu", includeUnavailable: false);
        Assert.Empty(activeOnly);
        var all = await menuSvc.GetProductsAsync(catId, "Bạc Xỉu", includeUnavailable: true);
        Assert.Single(all);

        // Soft delete
        Assert.True((await menuSvc.SoftDeleteProductAsync(customDrink.Data.Id)).Success);
        Assert.Null(await menuSvc.GetProductByIdAsync(customDrink.Data.Id));
        Assert.False((await menuSvc.SoftDeleteProductAsync(customDrink.Data.Id)).Success);
    }

    [Fact]
    public async Task MaxProducibleQuantity_ComputesLimitingIngredientAndFractionalRecipes()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var menuSvc = fx.CreateMenuService();
        var invSvc = fx.CreateInventoryService();

        var orange = (await invSvc.GetIngredientsAsync("Cam tươi")).Single();
        var tea = (await invSvc.GetIngredientsAsync("Trà túi lọc")).Single();

        // Drain orange to 3.5 quả -> Trà Đào (uses 0.5 quả + 1 túi) can make min(7, 200) = 7; Nước Ép Cam (2 quả) = 1
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            var oEntity = await db.Ingredients.FindAsync(orange.Id);
            oEntity!.Quantity = 3.5m;
            await db.SaveChangesAsync();
        }

        var products = await menuSvc.GetProductsAsync();
        var peachTea = products.Single(p => p.Name == "Trà Đào Cam Sả");
        var orangeJuice = products.Single(p => p.Name == "Nước Ép Cam");

        Assert.Equal(7, peachTea.MaxProducibleQuantity);
        Assert.Equal(1, orangeJuice.MaxProducibleQuantity);

        // Set tea stock to 0 -> peachTea becomes 0
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            var tEntity = await db.Ingredients.FindAsync(tea.Id);
            tEntity!.Quantity = 0m;
            await db.SaveChangesAsync();
        }

        var updatedPeach = await menuSvc.GetProductByIdAsync(peachTea.Id);
        Assert.Equal(0, updatedPeach!.MaxProducibleQuantity);
    }
}
