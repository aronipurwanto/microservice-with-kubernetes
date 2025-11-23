using Microsoft.EntityFrameworkCore;
using SharedModels.Models;

namespace OrderService.Models;

public class OrderContext : DbContext
{
    public OrderContext(DbContextOptions<OrderContext> options) : base(options) { }

    public DbSet<Order> Orders { get; set; }
    public DbSet<OrderItem> OrderItems { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Order>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.TotalAmount).HasPrecision(18, 2);
            entity.Property(e => e.Status)
                .HasConversion<string>(); // Store enum as string in database
            entity.Property(e => e.ShippingAddress).HasMaxLength(500);
            entity.Property(e => e.CustomerNotes).HasMaxLength(1000);
            
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Status);
            entity.HasIndex(e => e.OrderDate);
            entity.HasIndex(e => e.CreatedAt);

            entity.HasMany(o => o.OrderItems)
                .WithOne(oi => oi.Order)
                .HasForeignKey(oi => oi.OrderId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<OrderItem>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.UnitPrice).HasPrecision(18, 2);
            entity.Property(e => e.ProductName).HasMaxLength(255);
            entity.Property(e => e.ProductDescription).HasMaxLength(500);
            
            entity.HasIndex(e => e.OrderId);
            entity.HasIndex(e => e.ProductId);
        });
    }
}