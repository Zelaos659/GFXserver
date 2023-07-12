using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Security.Cryptography.X509Certificates;
using static GFXserver.Models;
using static GFXserver.test;
using static System.Net.Mime.MediaTypeNames;

namespace GFXserver
{
    public class Program
    {
        //public static IWebHostEnvironment _environment;
        //public ImageUploadController(IWebHostEnvironment environment)
        //{
        //    _environment = environment;
        //}
        public static void Main(string[] args)
        {
            #region settings
            
            var builder = WebApplication.CreateBuilder(args);

            builder.Logging.AddFile(Path.Combine(Directory.GetCurrentDirectory(), "logger.txt")).AddFilter("Microsoft.EntityFrameworkCore.Database.Command", LogLevel.Warning);

	        builder.WebHost.ConfigureKestrel(options =>
	        {
		        options.Listen(IPAddress.Any, 5003);
                options.Limits.MaxRequestBodySize = long.MaxValue;
            });

            builder.Services.Configure<IISServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            builder.Services.Configure<FormOptions>(options =>
            {
                options.MultipartBodyLengthLimit = int.MaxValue; // Устанавливает максимальный размер тела запроса
            });

            builder.Services.AddDbContext<ShopDB>(options =>
            {
                options.UseNpgsql(builder.Configuration.GetConnectionString("Npgsql"));
                //options.UseNpgsql(builder.Configuration.GetConnectionString("test"));
            });

            var app = builder.Build();

            app.UseForwardedHeaders(new ForwardedHeadersOptions
            {
                ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
            });

            app.UseStaticFiles();

            #endregion

            #region products
            
            // Загрузка файла

            app.MapPost("/uploadFile", async (HttpContext context) =>
            {
                // Получаем файл из запроса
                var file = context.Request.Form.Files.GetFile("file");

                if (file != null)
                {
                    // Сохраняем файл на сервере
                    var filePath = Path.Combine("/home/images/", file.FileName);
                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await file.CopyToAsync(stream);
                    }

                    // Возвращаем успешный результат
                    app.Logger.LogInformation($"POST Запрос \t На сервер загружен файл {file.FileName}");
                    return Results.Ok("Файл успешно загружен.");
                }

                // Если файл не найден, возвращаем ошибку
                return Results.BadRequest("Файл не найден.");
            });

            app.MapGet("/products", async (ShopDB db) =>
            {
                var products = await db.Products.ToListAsync();
                app.Logger.LogInformation($"GET Запрос \t Вывод всех продуктов");

                return products;
            });

            app.MapPost("/products/add", async (Product _product, ShopDB db) =>
            {
                app.Logger.LogInformation($"POST запрос \n Добавление продукта");
                string image = _product.Image;
                _product.Image = $"http://195.93.252.174:825/images/{image}";
                await db.Products.AddAsync(_product);
                await db.SaveChangesAsync();
            });
            
            app.MapGet("/lastProdId", async (ShopDB db) =>
            {
                var items = await db.Products.ToListAsync();
                return items.LastOrDefault();
            });

            app.MapPut("/products/edit", async (ShopDB db, Product _product) =>
            {
                var product = await db.Products.FindAsync(_product.Id);
                product.Name = _product.Name;
                product.Price = _product.Price;
                product.Category = _product.Category;
                product.Filter = _product.Filter;
                product.Description = _product.Description;
		if(product.Image != null)
		{
			product.Image = $"http://195.93.252.174:825/images/{_product.Image}";
		}
                await db.SaveChangesAsync();
                app.Logger.LogInformation($"PUT запрос \n Редактирование продукта");
                return true;
            });

            app.MapDelete("/products/delete/{id:int}", async (int id, ShopDB db) =>
            {
                var product = await db.Products.FindAsync(id);
                db.Products.Remove(product);
                await db.SaveChangesAsync();
                app.Logger.LogInformation($"DELETE запрос \n Удаление продукта");
                return true;
            });

            #endregion

            #region productSize

            app.MapPost("/productSize/add", async (ProductSize _product, ShopDB db) =>
            {
                var list = await db.ProductSizes.Where(p => p.ProductId == _product.ProductId).ToListAsync();
                if (list.Select(p => p.Size).Contains(_product.Size))
                {
                    var size = await db.ProductSizes.FirstOrDefaultAsync(p=>p.Size == _product.Size);
                    size.Stock = _product.Stock;
                    await db.SaveChangesAsync();
                    app.Logger.LogInformation($"Редактирование размеров");
                    return false;
                }
                app.Logger.LogInformation($"Добавление размера");
                await db.ProductSizes.AddAsync(_product);
                await db.SaveChangesAsync();
                return true;
                //await db.ProductSizes.AddAsync(_product);
            });

            app.MapGet("/list/{id:int}", async (ShopDB db, int id) =>
            {
                var items = await db.ProductSizes.Where(p => p.ProductId == id).ToListAsync();
                return items;
            });

            #endregion

            #region users

            app.MapPost("/user/login", async (ShopDB db, User _user) =>
            {
                var user = await db.Users.FirstOrDefaultAsync(p => p.Username == _user.Username && p.Password == _user.Password);
                if (user == null)
                {
                    app.Logger.LogInformation($"Пользователь не авторизирован");
                    return null;
                }
                app.Logger.LogInformation($"Пользователь авторизирован");
                return user;
            });

            app.MapPost("/user/register", async (User _user, ShopDB db) =>
            {
                var user = await db.Users.FirstOrDefaultAsync(p => p.Username == _user.Username);
                if (user != null)
                {
                    app.Logger.LogInformation($"POST запрос \t Пользователь не зарегистрирован");
                    return false;
                }
                app.Logger.LogInformation($"POST запрос \t Регистрация пользователя '{_user.Username}'");
                await db.Users.AddAsync(_user);
                await db.SaveChangesAsync();
                return true;
            });

            #endregion

            #region carts

            app.MapGet("/cart/{id:int}", async (ShopDB db, int id) =>
            {
                app.Logger.LogInformation("GET запрос \t Запрос корзины пользователя");
                var cart = await db.Carts
                .Where(c=>c.UserId == id)
                .Include(c => c.ProductSize)
                    .ThenInclude(ps=>ps.Product)
                .Include(c => c.User).ToListAsync();
                return cart;
            });

            app.MapPost("/cart/itemAdd", async (ShopDB db, Cart cart) =>
            {
                var list = await db.Carts.Where(p => p.UserId == cart.UserId).ToListAsync();
                
                if (list.Select(p => p.ProductSizeId).Contains(cart.ProductSizeId))
                {
                    app.Logger.LogInformation("POST запрос \t Товар уже существует в корзине");
                    return false;
                }
                await db.Carts.AddAsync(cart);
                app.Logger.LogInformation($"POST запрос \t Товар добавлен в корзину");
                await db.SaveChangesAsync();
                return true;
            });

            app.MapDelete("/cart/itemDelete/{id:int}", async (ShopDB db, int id) =>
            {
                var item = await db.Carts.FindAsync(id);
                db.Carts.Remove(item);
                app.Logger.LogInformation($"DELETE запрос \t Товар удалён из корзины");
                await db.SaveChangesAsync();
                return true;
            });

            #endregion

            #region orders

            app.MapPost("/order/create", async (ShopDB db, Order _order) =>
            {
                await db.Orders.AddAsync(_order);
                await db.SaveChangesAsync();
                app.Logger.LogInformation($"POST запрос \t Создание заказа");
            });
            
	        app.MapGet("/orders", async (ShopDB db) =>
	        {
                app.Logger.LogInformation($"GET запрос \t Запрос всех заказов");
                return await db.Orders.Include(u=>u.User).ToListAsync();
	        });

            app.MapGet("/order/get/{id:int}", async (ShopDB db, int id) =>
            {
                app.Logger.LogInformation($"GET запрос \t Запрос заказов пользователя с id: {id}");
                return await db.Orders.Where(p=>p.UserId == id).Include(p=>p.OrderItems).ToListAsync();
            });

            app.MapPut("/order/edit/{id:int}", async (ShopDB db, int id, Order _order) =>
            {
                var order = await db.Orders.FindAsync(id);
                order.DeliveryDate = _order.DeliveryDate;
                order.OrderStatus = _order.OrderStatus;
                await db.SaveChangesAsync();
                app.Logger.LogInformation($"PUT запрос \t Изменение заказа");
            });

            app.MapDelete("order/delete/{id:int}", async (ShopDB db, int id) =>
            {
                var order = await db.Orders.FindAsync(id);
                db.Orders.Remove(order);
                await db.SaveChangesAsync();
                app.Logger.LogInformation($"Delete запрос \t Удаление заказа");
            });

            #endregion

            #region orderItems

            app.MapGet("order/createItems/{id:int}", async (ShopDB db, int id) =>
            {
                List<OrderItem> orderItems = new List<OrderItem>(); // Лист с товарами для передачи в бд
                var order = await db.Orders.OrderBy(p=>p.Id).LastOrDefaultAsync(p=>p.UserId == id); // Для того чтобы узнать id последнего созданого заказа (т.е. этого)
                var list = await db.Carts.Where(u => u.UserId == id).ToListAsync(); // Список товаров из корзины
                for (int i = 0; i < list.Count; i++)
                {
                    var item = new OrderItem()
                    {
                        OrderId = order.Id,
                        ProductSizeId = list[i].ProductSizeId
                    };
                    orderItems.Add(item);
                }
                await db.OrderItems.AddRangeAsync(orderItems);
                db.Carts.RemoveRange(list);
                await db.SaveChangesAsync();
            });

            app.MapGet("orders/test/{id:int}", async (ShopDB db, int id) =>
            {
                var itemsitems = await db.OrderItems
                .Where(o => o.OrderId == id)
                .Include(o => o.Order)
                .Include(o => o.ProductSize)
                    .ThenInclude(o => o.Product)
                .ToListAsync();
                return itemsitems;
            });

            app.MapGet("orders/{id:int}", async (ShopDB db, int id) =>
            {
                var orders = await db.OrderItems
                .Where(o => o.Order.UserId == id)
                .Include(o => o.Order)
                .Include(o => o.ProductSize)
                    .ThenInclude(o=>o.Product)
                .ToListAsync();

                return orders;
            });

            #endregion

            app.Logger.LogInformation($"Сервер запущен");
            app.Run();
        }
    }
}
