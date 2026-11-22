namespace CoffeeShop.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int CategoryId { get; set; }
    public string? ImagePath { get; set; }
    public bool IsAvailable { get; set; } = true;
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;
    public DateTime UpdatedAt { get; set; } = DateTime.Now;

    public Category? Category { get; set; }
    public ICollection<RecipeItem> RecipeItems { get; set; } = new List<RecipeItem>();
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
