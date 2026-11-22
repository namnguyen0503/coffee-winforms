using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoffeeShop.Infrastructure.Data.Configurations;

public class UserConfiguration : IEntityTypeConfiguration<User>
{
    public void Configure(EntityTypeBuilder<User> builder)
    {
        builder.ToTable("Users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.FullName).IsRequired().HasMaxLength(120);
        builder.Property(x => x.Username).IsRequired().HasMaxLength(60);
        builder.HasIndex(x => x.Username).IsUnique();
        builder.Property(x => x.PasswordHash).IsRequired().HasMaxLength(255);
    }
}
