using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using static GFXserver.Models;

namespace GFXserver
{
    public class ShopDB : DbContext
    {
        public ShopDB(DbContextOptions<ShopDB> options) : base(options) {}
        public DbSet<Product> Products { get; set; }
        public DbSet<ProductSize> ProductSizes { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Cart> Carts { get; set; }
        public DbSet<Order> Orders { get; set; }
        public DbSet<OrderItem> OrderItems { get; set; }
        //public ShopDB()
        //{
            //Database.EnsureDeleted();
            //Database.EnsureCreated();
        //}
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.OrderDate)
                    .HasColumnType("date");
            });modelBuilder.Entity<Order>(entity =>
            {
                entity.Property(o => o.DeliveryDate)
                    .HasColumnType("date");
            });
            modelBuilder.Entity<Order>()
                .HasMany(o => o.OrderItems)
                .WithOne(o => o.Order)
                .HasForeignKey(o => o.OrderId);
        }


    }
}
