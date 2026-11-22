using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;

namespace CoffeeShop.Application.Interfaces;

public interface IInventoryService
{
    Task<IReadOnlyList<IngredientDto>> GetIngredientsAsync(string? search = null, bool lowStockOnly = false);
    Task<ServiceResult<IngredientDto>> AddIngredientAsync(AddIngredientRequest request, int userId);
    Task<ServiceResult> ImportStockAsync(ImportStockRequest request, int userId);
    Task<ServiceResult> ManualExportAsync(ManualExportRequest request, int userId);
    Task<ServiceResult<int>> UpdateMinStocksAsync(IReadOnlyList<MinStockUpdateItem> items);
    Task<IReadOnlyList<InventoryTransactionDto>> GetTransactionsAsync(InventoryLogFilterRequest filter);
}
