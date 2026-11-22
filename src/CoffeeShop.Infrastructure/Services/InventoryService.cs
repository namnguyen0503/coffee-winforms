using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Enums;
using CoffeeShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Services;

public class InventoryService : IInventoryService
{
    private readonly IDbContextFactory<CoffeeDbContext> _dbFactory;

    public InventoryService(IDbContextFactory<CoffeeDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(string? search = null, bool lowStockOnly = false)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Ingredients.AsNoTracking().AsQueryable();

        if (lowStockOnly)
            query = query.Where(i => i.Quantity <= i.MinQuantity);

        var items = await query
            .Select(i => new IngredientDto(
                i.Id,
                i.Name,
                i.Unit,
                i.Quantity,
                i.MinQuantity,
                i.AverageUnitCost,
                i.UpdatedAt))
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            items = items
                .Where(i => i.Name.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return items
            .OrderBy(i => i.IsLowStock ? 0 : 1)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<ServiceResult<IngredientDto>> AddIngredientAsync(AddIngredientRequest request, int userId)
    {
        var name = request.Name?.Trim() ?? string.Empty;
        var unit = request.Unit?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(name) || string.IsNullOrEmpty(unit))
            return ServiceResult<IngredientDto>.Fail("Tên nguyên liệu và đơn vị tính không được để trống.");
        if (request.InitialQuantity < 0 || request.MinQuantity < 0 || request.InitialTotalCost < 0)
            return ServiceResult<IngredientDto>.Fail("Số lượng và giá trị không được âm.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null || (user.Role != UserRole.Admin && user.Role != UserRole.WarehouseStaff))
            return ServiceResult<IngredientDto>.Fail("Bạn không có quyền thêm nguyên liệu.");

        var existingNames = await db.Ingredients.Select(i => i.Name).ToListAsync();
        if (existingNames.Any(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
            return ServiceResult<IngredientDto>.Fail($"Nguyên liệu '{name}' đã tồn tại!");

        var now = DateTime.Now;
        var avgCost = request.InitialQuantity > 0
            ? Math.Round(request.InitialTotalCost / request.InitialQuantity, 2)
            : 0m;

        var ingredient = new Ingredient
        {
            Name = name,
            Unit = unit,
            Quantity = request.InitialQuantity,
            MinQuantity = request.MinQuantity,
            AverageUnitCost = avgCost,
            UpdatedAt = now
        };

        db.Ingredients.Add(ingredient);
        await db.SaveChangesAsync();

        if (request.InitialQuantity > 0)
        {
            db.InventoryTransactions.Add(new InventoryTransaction
            {
                IngredientId = ingredient.Id,
                Type = InventoryTransactionType.Import,
                Quantity = request.InitialQuantity,
                TotalCost = request.InitialTotalCost,
                UnitCost = avgCost,
                Note = "Khởi tạo nguyên liệu mới",
                UserId = userId,
                CreatedAt = now
            });
            await db.SaveChangesAsync();
        }

        await tx.CommitAsync();

        return ServiceResult<IngredientDto>.Ok(
            new IngredientDto(
                ingredient.Id,
                ingredient.Name,
                ingredient.Unit,
                ingredient.Quantity,
                ingredient.MinQuantity,
                ingredient.AverageUnitCost,
                ingredient.UpdatedAt),
            "Thêm nguyên liệu mới thành công!");
    }

    public async Task<ServiceResult> ImportStockAsync(ImportStockRequest request, int userId)
    {
        if (request.IngredientId <= 0)
            return ServiceResult.Fail("Chưa chọn nguyên liệu.");
        if (request.QuantityToAdd <= 0)
            return ServiceResult.Fail("Số lượng nhập phải lớn hơn 0.");
        if (request.TotalCost < 0)
            return ServiceResult.Fail("Chi phí nhập không được âm.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null || (user.Role != UserRole.Admin && user.Role != UserRole.WarehouseStaff))
            return ServiceResult.Fail("Bạn không có quyền nhập kho.");

        var ing = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == request.IngredientId);
        if (ing is null)
            return ServiceResult.Fail("Nguyên liệu không tồn tại.");

        var now = DateTime.Now;
        var currentVal = ing.Quantity * ing.AverageUnitCost;
        var newQty = ing.Quantity + request.QuantityToAdd;
        var newAvgCost = newQty > 0
            ? Math.Round((currentVal + request.TotalCost) / newQty, 2)
            : ing.AverageUnitCost;
        var batchUnitCost = Math.Round(request.TotalCost / request.QuantityToAdd, 2);

        ing.Quantity = newQty;
        ing.AverageUnitCost = newAvgCost;
        ing.UpdatedAt = now;

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            IngredientId = ing.Id,
            Type = InventoryTransactionType.Import,
            Quantity = request.QuantityToAdd,
            TotalCost = request.TotalCost,
            UnitCost = batchUnitCost,
            Note = string.IsNullOrWhiteSpace(request.Note) ? "Nhập kho" : request.Note.Trim(),
            UserId = userId,
            CreatedAt = now
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return ServiceResult.Ok($"Đã nhập thêm {request.QuantityToAdd:N2} {ing.Unit} '{ing.Name}' thành công!");
    }

    public async Task<ServiceResult> ManualExportAsync(ManualExportRequest request, int userId)
    {
        if (request.IngredientId <= 0)
            return ServiceResult.Fail("Chưa chọn nguyên liệu.");
        if (request.QuantityToSubtract <= 0)
            return ServiceResult.Fail("Số lượng xuất phải lớn hơn 0.");
        if (string.IsNullOrWhiteSpace(request.Reason))
            return ServiceResult.Fail("Vui lòng nhập lý do xuất kho.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null || (user.Role != UserRole.Admin && user.Role != UserRole.WarehouseStaff))
            return ServiceResult.Fail("Chỉ Admin hoặc Thủ kho mới được xuất kho thủ công.");

        var ing = await db.Ingredients.FirstOrDefaultAsync(i => i.Id == request.IngredientId);
        if (ing is null)
            return ServiceResult.Fail("Nguyên liệu không tồn tại.");

        if (ing.Quantity < request.QuantityToSubtract)
            return ServiceResult.Fail($"Không đủ tồn kho để xuất! Hiện còn: {ing.Quantity:N2} {ing.Unit}.");

        var now = DateTime.Now;
        ing.Quantity -= request.QuantityToSubtract;
        ing.UpdatedAt = now;

        db.InventoryTransactions.Add(new InventoryTransaction
        {
            IngredientId = ing.Id,
            Type = InventoryTransactionType.ManualExport,
            Quantity = request.QuantityToSubtract,
            UnitCost = ing.AverageUnitCost,
            TotalCost = Math.Round(request.QuantityToSubtract * ing.AverageUnitCost, 2),
            Note = $"Xuất kho: {request.Reason.Trim()}",
            UserId = userId,
            CreatedAt = now
        });

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return ServiceResult.Ok($"Đã xuất {request.QuantityToSubtract:N2} {ing.Unit} '{ing.Name}'.");
    }

    public async Task<ServiceResult<int>> UpdateMinStocksAsync(IReadOnlyList<MinStockUpdateItem> items)
    {
        if (items.Count == 0)
            return ServiceResult<int>.Ok(0, "Không có dữ liệu thay đổi.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var ids = items.Select(x => x.IngredientId).Distinct().ToList();
        var ingredients = await db.Ingredients
            .Where(i => ids.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        var updated = 0;
        foreach (var item in items)
        {
            if (item.MinQuantity < 0)
                return ServiceResult<int>.Fail("Ngưỡng tối thiểu không được âm.");

            if (ingredients.TryGetValue(item.IngredientId, out var ing))
            {
                ing.MinQuantity = item.MinQuantity;
                ing.UpdatedAt = DateTime.Now;
                updated++;
            }
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return ServiceResult<int>.Ok(updated, $"Đã cập nhật ngưỡng tối thiểu cho {updated} nguyên liệu.");
    }

    public async Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(InventoryLogFilterRequest filter)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.InventoryTransactions.AsNoTracking()
            .Include(t => t.Ingredient)
            .Include(t => t.User)
            .AsQueryable();

        if (filter.StartDate.HasValue)
        {
            var startDt = filter.StartDate.Value.ToDateTime(TimeOnly.MinValue);
            query = query.Where(t => t.CreatedAt >= startDt);
        }

        if (filter.EndDate.HasValue)
        {
            var endDt = filter.EndDate.Value.ToDateTime(TimeOnly.MaxValue);
            query = query.Where(t => t.CreatedAt <= endDt);
        }

        if (filter.IngredientId.HasValue && filter.IngredientId.Value > 0)
            query = query.Where(t => t.IngredientId == filter.IngredientId.Value);

        if (filter.Type.HasValue)
            query = query.Where(t => t.Type == filter.Type.Value);

        if (filter.UserId.HasValue && filter.UserId.Value > 0)
            query = query.Where(t => t.UserId == filter.UserId.Value);

        var page = Math.Max(1, filter.Page);
        var size = Math.Clamp(filter.PageSize, 1, 500);

        return await query
            .OrderByDescending(t => t.Id)
            .Skip((page - 1) * size)
            .Take(size)
            .Select(t => new InventoryTransactionDto(
                t.Id,
                t.IngredientId,
                t.Ingredient != null ? t.Ingredient.Name : "N/A",
                t.Ingredient != null ? t.Ingredient.Unit : string.Empty,
                t.Type,
                t.Quantity,
                t.TotalCost,
                t.UnitCost,
                t.Note,
                t.ReferenceOrderId,
                t.User != null ? t.User.FullName : "N/A",
                t.CreatedAt))
            .ToListAsync();
    }
}
