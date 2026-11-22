using CoffeeShop.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CoffeeShop.Infrastructure.Data.Configurations;

public class WorkSessionConfiguration : IEntityTypeConfiguration<WorkSession>
{
    public void Configure(EntityTypeBuilder<WorkSession> builder)
    {
        builder.ToTable("WorkSessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.StartCash).HasPrecision(18, 2);
        builder.Property(x => x.EndCash).HasPrecision(18, 2);
        builder.Property(x => x.CashSales).HasPrecision(18, 2);
        builder.Property(x => x.TransferSales).HasPrecision(18, 2);
        builder.Property(x => x.TotalSales).HasPrecision(18, 2);
        builder.Property(x => x.ExpectedCash).HasPrecision(18, 2);
        builder.Property(x => x.CashVariance).HasPrecision(18, 2);
        builder.Property(x => x.Note).HasMaxLength(500);

        builder.HasOne(x => x.User)
            .WithMany(u => u.WorkSessions)
            .HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
