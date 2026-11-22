using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Services;

public class MenuService : IMenuService
{
    private readonly IDbContextFactory<CoffeeDbContext> _dbFactory;

    public MenuService(IDbContextFactory<CoffeeDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Categories.AsNoTracking()
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Id)
            .Select(c => new CategoryDto(c.Id, c.Name, c.SortOrder))
            .ToListAsync();
    }

    public async Task<ServiceResult<CategoryDto>> SaveCategoryAsync(int? id, string name, int sortOrder)
    {
        var trimmed = name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(trimmed))
            return ServiceResult<CategoryDto>.Fail("Tên danh mục không được để trống.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var existingCats = await db.Categories
            .Select(c => new { c.Id, c.Name })
            .ToListAsync();
        var duplicate = existingCats.Any(c =>
            string.Equals(c.Name, trimmed, StringComparison.OrdinalIgnoreCase) &&
            (!id.HasValue || c.Id != id.Value));
        if (duplicate)
            return ServiceResult<CategoryDto>.Fail("Tên danh mục đã tồn tại.");

        Category entity;
        if (id.HasValue)
        {
            var found = await db.Categories.FirstOrDefaultAsync(c => c.Id == id.Value);
            if (found is null)
                return ServiceResult<CategoryDto>.Fail("Không tìm thấy danh mục.");
            entity = found;
            entity.Name = trimmed;
            entity.SortOrder = sortOrder;
        }
        else
        {
            entity = new Category { Name = trimmed, SortOrder = sortOrder };
            db.Categories.Add(entity);
        }

        await db.SaveChangesAsync();
        return ServiceResult<CategoryDto>.Ok(
            new CategoryDto(entity.Id, entity.Name, entity.SortOrder),
            "Lưu danh mục thành công!");
    }

    public async Task<IReadOnlyList<ProductDto>> GetProductsAsync(
        int? categoryId = null,
        string? search = null,
        bool includeUnavailable = true)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.RecipeItems)
            .ThenInclude(r => r.Ingredient)
            .Where(p => !p.IsDeleted);

        if (categoryId.HasValue && categoryId.Value > 0)
            query = query.Where(p => p.CategoryId == categoryId.Value);

        if (!includeUnavailable)
            query = query.Where(p => p.IsAvailable);

        var products = await query
            .OrderBy(p => p.CategoryId)
            .ThenBy(p => p.Name)
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            products = products
                .Where(p => p.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return products.Select(MapProductDto).ToList();
    }

    public async Task<ProductDto?> GetProductByIdAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var product = await db.Products.AsNoTracking()
            .Include(p => p.Category)
            .Include(p => p.RecipeItems)
            .ThenInclude(r => r.Ingredient)
            .FirstOrDefaultAsync(p => p.Id == id && !p.IsDeleted);

        return product is null ? null : MapProductDto(product);
    }

    public async Task<ServiceResult<ProductDto>> SaveProductAsync(SaveProductRequest request)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(name))
            return ServiceResult<ProductDto>.Fail("Tên món không được để trống.");
        if (request.Price < 0)
            return ServiceResult<ProductDto>.Fail("Giá bán không được âm.");

        var mergedRecipes = request.Recipes
            .Where(r => r.IngredientId > 0 && r.QuantityRequired > 0)
            .GroupBy(r => r.IngredientId)
            .Select(g => new RecipeLineRequest(g.Key, g.Sum(x => x.QuantityRequired)))
            .ToList();

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var categoryExists = await db.Categories.AnyAsync(c => c.Id == request.CategoryId);
        if (!categoryExists)
            return ServiceResult<ProductDto>.Fail("Danh mục không tồn tại.");

        if (mergedRecipes.Count > 0)
        {
            var reqIngIds = mergedRecipes.Select(r => r.IngredientId).ToList();
            var existingIngCount = await db.Ingredients.CountAsync(i => reqIngIds.Contains(i.Id));
            if (existingIngCount != reqIngIds.Count)
                return ServiceResult<ProductDto>.Fail("Một hoặc nhiều nguyên liệu trong công thức không tồn tại.");
        }

        var now = DateTime.Now;
        Product product;

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            var existing = await db.Products
                .Include(p => p.RecipeItems)
                .FirstOrDefaultAsync(p => p.Id == request.Id.Value && !p.IsDeleted);

            if (existing is null)
                return ServiceResult<ProductDto>.Fail("Không tìm thấy sản phẩm cần cập nhật.");

            existing.Name = name;
            existing.Price = request.Price;
            existing.CategoryId = request.CategoryId;
            if (!string.IsNullOrWhiteSpace(request.ImagePath))
                existing.ImagePath = request.ImagePath;
            existing.IsAvailable = request.IsAvailable;
            existing.UpdatedAt = now;

            db.RecipeItems.RemoveRange(existing.RecipeItems);
            product = existing;
        }
        else
        {
            product = new Product
            {
                Name = name,
                Price = request.Price,
                CategoryId = request.CategoryId,
                ImagePath = string.IsNullOrWhiteSpace(request.ImagePath) ? null : request.ImagePath,
                IsAvailable = request.IsAvailable,
                IsDeleted = false,
                CreatedAt = now,
                UpdatedAt = now
            };
            db.Products.Add(product);
        }

        await db.SaveChangesAsync();

        foreach (var line in mergedRecipes)
        {
            db.RecipeItems.Add(new RecipeItem
            {
                ProductId = product.Id,
                IngredientId = line.IngredientId,
                QuantityRequired = line.QuantityRequired
            });
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        var saved = await GetProductByIdAsync(product.Id);
        return ServiceResult<ProductDto>.Ok(saved!, request.Id.HasValue ? "Cập nhật món thành công!" : "Thêm món thành công!");
    }

    public async Task<ServiceResult> SoftDeleteProductAsync(int productId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var product = await db.Products.FirstOrDefaultAsync(p => p.Id == productId && !p.IsDeleted);
        if (product is null)
            return ServiceResult.Fail("Không tìm thấy món.");

        product.IsDeleted = true;
        product.IsAvailable = false;
        product.UpdatedAt = DateTime.Now;
        await db.SaveChangesAsync();

        return ServiceResult.Ok("Đã xóa món khỏi thực đơn.");
    }

    private static ProductDto MapProductDto(Product p)
    {
        int? maxQty = null;
        if (p.RecipeItems.Count > 0)
        {
            var computedMax = int.MaxValue;
            foreach (var r in p.RecipeItems)
            {
                if (r.QuantityRequired <= 0 || r.Ingredient is null)
                    continue;

                var possible = (int)Math.Floor(r.Ingredient.Quantity / r.QuantityRequired);
                if (possible < computedMax)
                    computedMax = possible;
            }

            maxQty = computedMax == int.MaxValue ? 0 : Math.Max(0, computedMax);
        }

        var unitCost = p.RecipeItems.Sum(r =>
            r.Ingredient is null ? 0m : Math.Round(r.QuantityRequired * r.Ingredient.AverageUnitCost, 2));

        var recipeDtos = p.RecipeItems
            .Select(r => new RecipeItemDto(
                r.IngredientId,
                r.Ingredient?.Name ?? "N/A",
                r.Ingredient?.Unit ?? string.Empty,
                r.QuantityRequired,
                r.Ingredient?.AverageUnitCost ?? 0m))
            .OrderBy(r => r.IngredientName)
            .ToList();

        return new ProductDto(
            p.Id,
            p.Name,
            p.Price,
            p.CategoryId,
            p.Category?.Name ?? string.Empty,
            p.ImagePath,
            p.IsAvailable,
            maxQty,
            unitCost,
            recipeDtos);
    }
}
