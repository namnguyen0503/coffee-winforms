using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoffeeShop.Infrastructure.Data.Configurations;

public class IngredientConfiguration : IEntityTypeConfiguration<Ingredient>
{
    public void Configure(EntityTypeBuilder<Ingredient> builder)
    {
        builder.ToTable("Ingredients");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.HasIndex(x => x.Name).IsUnique();
        builder.Property(x => x.Unit).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Quantity).HasPrecision(18, 3);
        builder.Property(x => x.MinQuantity).HasPrecision(18, 3);
        builder.Property(x => x.AverageUnitCost).HasPrecision(18, 2);
    }
}
