namespace CoffeeShop.Application.DTOs;

public record CategoryDto(int Id, string Name, int SortOrder);

public record RecipeItemDto(
    int IngredientId,
    string IngredientName,
    string Unit,
    decimal QuantityRequired,
    decimal IngredientUnitCost);

public record ProductDto(
    int Id,
    string Name,
    decimal Price,
    int CategoryId,
    string CategoryName,
    string? ImagePath,
    bool IsAvailable,
    int? MaxProducibleQuantity,
    decimal EstimatedUnitCost,
    IReadOnlyList<RecipeItemDto> Recipes);

public record RecipeLineRequest(int IngredientId, decimal QuantityRequired);

public record SaveProductRequest(
    int? Id,
    string Name,
    decimal Price,
    int CategoryId,
    string? ImagePath,
    bool IsAvailable,
    IReadOnlyList<RecipeLineRequest> Recipes);
