using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace GFXserver
{
    public class Models
    {
        public class Product
        {
            public int Id { get; set; }
            public string Category { get; set; }
            public string Filter { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public string Image { get; set; }
            
        }

        public class ProductSize
        {
            public int Id { get; set; }
            public int ProductId { get; set; }
            public string Size { get; set; }
            public int Stock { get; set; }
            public virtual Product Product { get; set; }
            [JsonIgnore]
            public virtual ICollection<Cart> Carts { get; set; }
            [JsonIgnore]
            public virtual ICollection<OrderItem> OrderItems { get; set; }
        }

        public class User
        {
            public int Id { get; set; }
            public string FirstName { get; set; }
            public string LastName { get; set; }
            public string PhoneNumber { get; set; }
            public string Username { get; set; }
            public string Password { get; set; }
            public string Access { get; set; }
            [JsonIgnore]
            public virtual ICollection<Cart> Carts { get; set; }
            [JsonIgnore]
            public virtual ICollection<Order> Orders { get; set; }
        }
        public class Order
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public string OrderStatus { get; set; }
            public DateTime OrderDate { get; set; }
            public DateTime DeliveryDate { get; set; }
            public string City { get; set; }
            public string Address { get; set; }
            
            [JsonIgnore]
            public List<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
            public virtual User User { get; set; }
        }
        public class OrderItem
        {
            public int Id { get; set; }
            public int OrderId { get; set; }
            public int ProductSizeId { get; set; } //was ProductId
            
            public virtual Order Order { get; set; }
            public virtual ProductSize ProductSize { get; set; }
        }
        public class Cart
        {
            public int Id { get; set; }
            public int UserId { get; set; }
            public int ProductSizeId { get; set; }
            public virtual ProductSize ProductSize { get; set; }
            public virtual User User { get; set; }
        }
    }
}
