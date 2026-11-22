using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoffeeShop.Infrastructure.Data.Configurations;

public class RecipeItemConfiguration : IEntityTypeConfiguration<RecipeItem>
{
    public void Configure(EntityTypeBuilder<RecipeItem> builder)
    {
        builder.ToTable("RecipeItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.QuantityRequired).HasPrecision(18, 3);
        builder.HasIndex(x => new { x.ProductId, x.IngredientId }).IsUnique();

        builder.HasOne(x => x.Product)
            .WithMany(p => p.RecipeItems)
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Ingredient)
            .WithMany(i => i.RecipeItems)
            .HasForeignKey(x => x.IngredientId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
