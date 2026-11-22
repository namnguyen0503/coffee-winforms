using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;

namespace CoffeeShop.Application.Interfaces;

public interface IMenuService
{
    Task<IReadOnlyList<CategoryDto>> GetCategoriesAsync();
    Task<ServiceResult<CategoryDto>> SaveCategoryAsync(int? id, string name, int sortOrder);
    Task<IReadOnlyList<ProductDto>> GetProductsAsync(int? categoryId = null, string? search = null, bool includeUnavailable = true);
    Task<ProductDto?> GetProductByIdAsync(int id);
    Task<ServiceResult<ProductDto>> SaveProductAsync(SaveProductRequest request);
    Task<ServiceResult> SoftDeleteProductAsync(int productId);
}
