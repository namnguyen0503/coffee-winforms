using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(CoffeeDbContext db)
    {
        if (await db.Users.AnyAsync())
            return;

        var now = DateTime.Now;
        var defaultPasswordHash = BCrypt.Net.BCrypt.HashPassword("123456");

        var admin = new User
        {
            FullName = "Quản trị viên",
            Username = "admin",
            PasswordHash = defaultPasswordHash,
            Role = UserRole.Admin,
            IsActive = true,
            CreatedAt = now
        };
        var staff = new User
        {
            FullName = "Nhân viên Thu ngân",
            Username = "staff",
            PasswordHash = defaultPasswordHash,
            Role = UserRole.Staff,
            IsActive = true,
            CreatedAt = now
        };
        var warehouse = new User
        {
            FullName = "Thủ kho",
            Username = "thukho",
            PasswordHash = defaultPasswordHash,
            Role = UserRole.WarehouseStaff,
            IsActive = true,
            CreatedAt = now
        };

        db.Users.AddRange(admin, staff, warehouse);
        await db.SaveChangesAsync();

        var catDrink = new Category { Name = "Đồ uống", SortOrder = 1 };
        var catFood = new Category { Name = "Đồ ăn", SortOrder = 2 };
        db.Categories.AddRange(catDrink, catFood);
        await db.SaveChangesAsync();

        var ingredients = new List<Ingredient>
        {
            new() { Name = "Hạt Cà phê", Unit = "g", Quantity = 5000m, MinQuantity = 500m, AverageUnitCost = 220m, UpdatedAt = now },
            new() { Name = "Sữa tươi", Unit = "ml", Quantity = 20000m, MinQuantity = 2000m, AverageUnitCost = 30m, UpdatedAt = now },
            new() { Name = "Trà túi lọc (Đào)", Unit = "túi", Quantity = 200m, MinQuantity = 20m, AverageUnitCost = 2500m, UpdatedAt = now },
            new() { Name = "Cam tươi", Unit = "quả", Quantity = 100m, MinQuantity = 15m, AverageUnitCost = 5000m, UpdatedAt = now },
            new() { Name = "Bơ sáp", Unit = "g", Quantity = 5000m, MinQuantity = 500m, AverageUnitCost = 60m, UpdatedAt = now },
            new() { Name = "Bột mì", Unit = "g", Quantity = 10000m, MinQuantity = 1000m, AverageUnitCost = 20m, UpdatedAt = now },
            new() { Name = "Chocolate", Unit = "g", Quantity = 3000m, MinQuantity = 300m, AverageUnitCost = 200m, UpdatedAt = now },
            new() { Name = "Khoai tây đông lạnh", Unit = "g", Quantity = 8000m, MinQuantity = 1000m, AverageUnitCost = 50m, UpdatedAt = now },
            new() { Name = "Bánh mì ổ", Unit = "ổ", Quantity = 80m, MinQuantity = 10m, AverageUnitCost = 2500m, UpdatedAt = now },
            new() { Name = "Bơ lạt", Unit = "g", Quantity = 2000m, MinQuantity = 200m, AverageUnitCost = 200m, UpdatedAt = now },
            new() { Name = "Tỏi", Unit = "g", Quantity = 500m, MinQuantity = 50m, AverageUnitCost = 50m, UpdatedAt = now },
            new() { Name = "Lá Parsley khô", Unit = "g", Quantity = 150m, MinQuantity = 20m, AverageUnitCost = 500m, UpdatedAt = now },
            new() { Name = "Sữa đặc", Unit = "ml", Quantity = 5000m, MinQuantity = 500m, AverageUnitCost = 50m, UpdatedAt = now }
        };
        db.Ingredients.AddRange(ingredients);
        await db.SaveChangesAsync();

        foreach (var ing in ingredients)
        {
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                IngredientId = ing.Id,
                Type = InventoryTransactionType.Import,
                Quantity = ing.Quantity,
                TotalCost = Math.Round(ing.Quantity * ing.AverageUnitCost, 2),
                UnitCost = ing.AverageUnitCost,
                Note = "Khởi tạo tồn kho ban đầu",
                UserId = admin.Id,
                CreatedAt = now
            });
        }

        var products = new List<Product>
        {
            new() { Name = "Cà phê Đen", Price = 25000m, CategoryId = catDrink.Id, ImagePath = "Assets/Images/drink/ca-phe-den.jpg", IsAvailable = true, CreatedAt = now, UpdatedAt = now },
            new() { Name = "Latte Đá", Price = 45000m, CategoryId = catDrink.Id, ImagePath = "Assets/Images/drink/latte-da.jpg", IsAvailable = true, CreatedAt = now, UpdatedAt = now },
            new() { Name = "Trà Đào Cam Sả", Price = 39000m, CategoryId = catDrink.Id, ImagePath = "Assets/Images/drink/tra-dao-cam-sa.jpg", IsAvailable = true, CreatedAt = now, UpdatedAt = now },
            new() { Name = "Nước Ép Cam", Price = 40000m, CategoryId = catDrink.Id, ImagePath = "Assets/Images/drink/nuoc-ep-cam.png", IsAvailable = true, CreatedAt = now, UpdatedAt = now },
            new() { Name = "Sinh Tố Bơ", Price = 55000m, CategoryId = catDrink.Id, ImagePath = "Assets/Images/drink/1766636352_bo_pc.jpg", IsAvailable = true, CreatedAt = now, UpdatedAt = now },
            new() { Name = "Bánh Mousse Chanh Leo", Price = 34000m, CategoryId = catFood.Id, ImagePath = "Assets/Images/food/banh-mousse-chanh-leo.png", IsAvailable = true, CreatedAt = now, UpdatedAt = now },
            new() { Name = "Bánh Mì Bơ Tỏi", Price = 29000m, CategoryId = catFood.Id, ImagePath = "Assets/Images/food/1766626403_bo_pc.jpg", IsAvailable = true, CreatedAt = now, UpdatedAt = now },
            new() { Name = "Bánh Muffin Chocolate", Price = 32000m, CategoryId = catFood.Id, ImagePath = "Assets/Images/food/banh-muffin-chocolate.jpg", IsAvailable = true, CreatedAt = now, UpdatedAt = now },
            new() { Name = "Khoai Tây Chiên", Price = 45000m, CategoryId = catFood.Id, ImagePath = "Assets/Images/food/khoai-tay-chien.jpg", IsAvailable = true, CreatedAt = now, UpdatedAt = now },
            new() { Name = "Bánh Tiramisu", Price = 30000m, CategoryId = catFood.Id, ImagePath = "Assets/Images/food/banh-tiramisu.png", IsAvailable = true, CreatedAt = now, UpdatedAt = now }
        };
        db.Products.AddRange(products);
        await db.SaveChangesAsync();

        var ingMap = ingredients.ToDictionary(x => x.Name, x => x.Id);
        var prodMap = products.ToDictionary(x => x.Name, x => x.Id);

        var recipes = new List<RecipeItem>
        {
            new() { ProductId = prodMap["Cà phê Đen"], IngredientId = ingMap["Hạt Cà phê"], QuantityRequired = 20m },
            new() { ProductId = prodMap["Latte Đá"], IngredientId = ingMap["Hạt Cà phê"], QuantityRequired = 20m },
            new() { ProductId = prodMap["Latte Đá"], IngredientId = ingMap["Sữa tươi"], QuantityRequired = 150m },
            new() { ProductId = prodMap["Trà Đào Cam Sả"], IngredientId = ingMap["Trà túi lọc (Đào)"], QuantityRequired = 1m },
            new() { ProductId = prodMap["Trà Đào Cam Sả"], IngredientId = ingMap["Cam tươi"], QuantityRequired = 0.5m },
            new() { ProductId = prodMap["Nước Ép Cam"], IngredientId = ingMap["Cam tươi"], QuantityRequired = 2m },
            new() { ProductId = prodMap["Sinh Tố Bơ"], IngredientId = ingMap["Bơ sáp"], QuantityRequired = 150m },
            new() { ProductId = prodMap["Sinh Tố Bơ"], IngredientId = ingMap["Sữa đặc"], QuantityRequired = 30m },
            new() { ProductId = prodMap["Bánh Mousse Chanh Leo"], IngredientId = ingMap["Bột mì"], QuantityRequired = 50m },
            new() { ProductId = prodMap["Bánh Mousse Chanh Leo"], IngredientId = ingMap["Sữa tươi"], QuantityRequired = 100m },
            new() { ProductId = prodMap["Bánh Mì Bơ Tỏi"], IngredientId = ingMap["Bánh mì ổ"], QuantityRequired = 1m },
            new() { ProductId = prodMap["Bánh Mì Bơ Tỏi"], IngredientId = ingMap["Bơ lạt"], QuantityRequired = 25m },
            new() { ProductId = prodMap["Bánh Mì Bơ Tỏi"], IngredientId = ingMap["Tỏi"], QuantityRequired = 10m },
            new() { ProductId = prodMap["Bánh Mì Bơ Tỏi"], IngredientId = ingMap["Lá Parsley khô"], QuantityRequired = 1m },
            new() { ProductId = prodMap["Bánh Mì Bơ Tỏi"], IngredientId = ingMap["Sữa đặc"], QuantityRequired = 15m },
            new() { ProductId = prodMap["Bánh Muffin Chocolate"], IngredientId = ingMap["Bột mì"], QuantityRequired = 100m },
            new() { ProductId = prodMap["Bánh Muffin Chocolate"], IngredientId = ingMap["Chocolate"], QuantityRequired = 30m },
            new() { ProductId = prodMap["Khoai Tây Chiên"], IngredientId = ingMap["Khoai tây đông lạnh"], QuantityRequired = 200m },
            new() { ProductId = prodMap["Bánh Tiramisu"], IngredientId = ingMap["Bột mì"], QuantityRequired = 50m },
            new() { ProductId = prodMap["Bánh Tiramisu"], IngredientId = ingMap["Sữa tươi"], QuantityRequired = 50m },
            new() { ProductId = prodMap["Bánh Tiramisu"], IngredientId = ingMap["Hạt Cà phê"], QuantityRequired = 20m }
        };
        db.RecipeItems.AddRange(recipes);

        db.Vouchers.AddRange(
            new Voucher { Code = "WELCOME", DiscountPercent = 10m, MinOrderAmount = 0m, Description = "Giảm 10% cho khách mới", IsActive = true, CreatedAt = now },
            new Voucher { Code = "VIP20", DiscountPercent = 20m, MinOrderAmount = 50000m, Description = "Giảm 20% đơn từ 50.000đ", IsActive = true, CreatedAt = now },
            new Voucher { Code = "ADMINVIP", DiscountPercent = 100m, MinOrderAmount = 0m, Description = "Mã ưu đãi đặc quyền Quản lý (tùy chỉnh %)", IsActive = true, CreatedAt = now }
        );

        var today = DateOnly.FromDateTime(now);
        for (int dayOffset = 0; dayOffset < 7; dayOffset++)
        {
            var date = today.AddDays(dayOffset);
            db.WorkSchedules.AddRange(
                new WorkSchedule { UserId = staff.Id, ShiftDate = date, ShiftType = ShiftType.Morning, CreatedAt = now },
                new WorkSchedule { UserId = staff.Id, ShiftDate = date, ShiftType = ShiftType.Afternoon, CreatedAt = now },
                new WorkSchedule { UserId = staff.Id, ShiftDate = date, ShiftType = ShiftType.Evening, CreatedAt = now }
            );
        }

        await db.SaveChangesAsync();
    }
}
