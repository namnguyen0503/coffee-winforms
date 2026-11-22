using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Domain.Entities;

public class InventoryTransaction
{
    public int Id { get; set; }
    public int IngredientId { get; set; }
    public InventoryTransactionType Type { get; set; }
    public decimal Quantity { get; set; }
    public decimal TotalCost { get; set; }
    public decimal UnitCost { get; set; }
    public string? Note { get; set; }
    public int? ReferenceOrderId { get; set; }
    public int UserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public Ingredient? Ingredient { get; set; }
    public User? User { get; set; }
}
