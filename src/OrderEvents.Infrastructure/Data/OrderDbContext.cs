using Microsoft.EntityFrameworkCore;

namespace OrderEvents.Infrastructure.Data;

public sealed class OrderDbContext(DbContextOptions<OrderDbContext> options) : DbContext(options)
{
    public DbSet<Order> Orders => Set<Order>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        var order = modelBuilder.Entity<Order>();

        order.ToTable("Orders");
        order.HasKey(o => o.Id);

        order.Property(o => o.CustomerId).HasMaxLength(64).IsRequired();
        order.Property(o => o.Sku).HasMaxLength(64).IsRequired();
        order.Property(o => o.TotalAmount).HasPrecision(18, 2);
        order.Property(o => o.Status).HasConversion<int>();
        order.Property(o => o.StatusReason).HasMaxLength(256);
        order.Property(o => o.LastProcessedCorrelationId).HasMaxLength(64);

        // Customer order history is the most common query after a point lookup.
        order.HasIndex(o => new { o.CustomerId, o.PlacedAt });
        order.HasIndex(o => o.Status);
    }
}
