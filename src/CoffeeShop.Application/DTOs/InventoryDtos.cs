using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Application.DTOs;

public record IngredientDto(
    int Id,
    string Name,
    string Unit,
    decimal Quantity,
    decimal MinQuantity,
    decimal AverageUnitCost,
    DateTime UpdatedAt)
{
    public bool IsLowStock => Quantity <= MinQuantity;
}

public record AddIngredientRequest(
    string Name,
    string Unit,
    decimal InitialQuantity,
    decimal MinQuantity,
    decimal InitialTotalCost);

public record ImportStockRequest(
    int IngredientId,
    decimal QuantityToAdd,
    decimal TotalCost,
    string? Note);

public record ManualExportRequest(
    int IngredientId,
    decimal QuantityToSubtract,
    string Reason);

public record MinStockUpdateItem(int IngredientId, decimal MinQuantity);

public record InventoryTransactionDto(
    int Id,
    int IngredientId,
    string IngredientName,
    string Unit,
    InventoryTransactionType Type,
    decimal Quantity,
    decimal TotalCost,
    decimal UnitCost,
    string? Note,
    int? ReferenceOrderId,
    string PerformedBy,
    DateTime CreatedAt);

public record InventoryLogFilterRequest(
    DateOnly? StartDate = null,
    DateOnly? EndDate = null,
    int? IngredientId = null,
    InventoryTransactionType? Type = null,
    int? UserId = null,
    int Page = 1,
    int PageSize = 50);
