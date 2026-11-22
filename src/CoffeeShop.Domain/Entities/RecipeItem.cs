namespace CoffeeShop.Domain.Entities;

public class RecipeItem
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public int IngredientId { get; set; }
    public decimal QuantityRequired { get; set; }

    public Product? Product { get; set; }
    public Ingredient? Ingredient { get; set; }
}
