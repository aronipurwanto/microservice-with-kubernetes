using Microsoft.AspNetCore.Mvc;
using Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure JSON to handle cycles
builder.Services.Configure<Microsoft.AspNetCore.Http.Json.JsonOptions>(options =>
{
    options.SerializerOptions.ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.IgnoreCycles;
});

var app = builder.Build();

// Configure HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Dummy Data
var products = new List<Product>
{
    new() { Id = 1, Name = "Laptop", Price = 999.99m, Stock = 10 },
    new() { Id = 2, Name = "Mouse", Price = 29.99m, Stock = 50 },
    new() { Id = 3, Name = "Keyboard", Price = 79.99m, Stock = 30 },
    new() { Id = 4, Name = "Monitor", Price = 199.99m, Stock = 15 }
};

// Endpoints
app.MapGet("/products", () => Results.Ok(products))
    .WithName("GetAllProducts")
    .WithOpenApi();

app.MapGet("/products/{id}", (int id) =>
    {
        var product = products.FirstOrDefault(p => p.Id == id);
        return product != null ? Results.Ok(product) : Results.NotFound();
    })
    .WithName("GetProductById")
    .WithOpenApi();

app.MapGet("/", () => "Product Service is running!")
    .WithName("HealthCheck")
    .WithOpenApi();

// Service Discovery Registration
app.MapGet("/service-info", () => new
{
    ServiceName = "ProductService",
    Status = "Running",
    Port = app.Urls.FirstOrDefault()?.Split(':').Last() ?? "unknown",
    Endpoints = new[] { "/products", "/products/{id}" }
});

app.Run();