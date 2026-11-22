using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoffeeShop.Infrastructure.Data.Configurations;

public class OrderConfiguration : IEntityTypeConfiguration<Order>
{
    public void Configure(EntityTypeBuilder<Order> builder)
    {
        builder.ToTable("Orders");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.OrderCode).IsRequired().HasMaxLength(30);
        builder.HasIndex(x => x.OrderCode).IsUnique();
        builder.Property(x => x.Subtotal).HasPrecision(18, 2);
        builder.Property(x => x.VoucherCode).HasMaxLength(50);
        builder.Property(x => x.DiscountPercent).HasPrecision(5, 2);
        builder.Property(x => x.DiscountAmount).HasPrecision(18, 2);
        builder.Property(x => x.FinalAmount).HasPrecision(18, 2);
        builder.Property(x => x.CancelReason).HasMaxLength(255);

        builder.HasOne(x => x.User)
            .WithMany(u => u.Orders)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.CancelledByUser)
            .WithMany(u => u.CancelledOrders)
            .HasForeignKey(x => x.CancelledByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(x => x.WorkSession)
            .WithMany(ws => ws.Orders)
            .HasForeignKey(x => x.WorkSessionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
