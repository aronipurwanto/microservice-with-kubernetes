using Microsoft.EntityFrameworkCore;
using ProductService.Models;
using ProductService.Services;
using SharedModels.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Entity Framework with SQL Server
builder.Services.AddDbContext<ProductContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register services
builder.Services.AddScoped<IProductService, ProductService.Services.ProductService>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<ProductContextHealthCheck>("ProductContext");


// Configure CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("AllowAll");
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Add health check endpoint
app.MapHealthChecks("/health");

// Initialize database
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ProductContext>();
    context.Database.EnsureCreated();
    
    // Seed sample data if needed
    await SeedSampleData(context);
}

app.Run();

// Seed sample data method
static async Task SeedSampleData(ProductContext context)
{
    if (!context.Categories.Any())
    {
        var categories = new[]
        {
            new Category { Id = Guid.NewGuid(), Name = "Books", Description = "Various books and publications" },
            new Category { Id = Guid.NewGuid(), Name = "Electronics", Description = "Electronic devices and accessories" },
            new Category { Id = Guid.NewGuid(), Name = "Clothing", Description = "Apparel and fashion items" }
        };

        await context.Categories.AddRangeAsync(categories);
        await context.SaveChangesAsync();

        // Add sample products if no products exist
        if (!context.Products.Any())
        {
            var booksCategory = categories[0];
            var products = new[]
            {
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "ASP.NET Core in Action",
                    Description = "Comprehensive guide to ASP.NET Core development",
                    Price = 49.99m,
                    StockQuantity = 100,
                    CategoryId = booksCategory.Id,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Microservices Architecture Patterns",
                    Description = "Learn microservices patterns and best practices",
                    Price = 59.99m,
                    StockQuantity = 75,
                    CategoryId = booksCategory.Id,
                    CreatedAt = DateTime.UtcNow
                },
                new Product
                {
                    Id = Guid.NewGuid(),
                    Name = "Docker and Kubernetes Guide",
                    Description = "Complete guide to containerization and orchestration",
                    Price = 54.99m,
                    StockQuantity = 50,
                    CategoryId = booksCategory.Id,
                    CreatedAt = DateTime.UtcNow
                }
            };

            await context.Products.AddRangeAsync(products);
            await context.SaveChangesAsync();
        }
    }
}