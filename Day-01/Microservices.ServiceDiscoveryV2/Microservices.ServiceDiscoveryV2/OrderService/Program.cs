using Microsoft.AspNetCore.Mvc;
using Shared.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddHttpClient();

// Configure JSON
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
var orders = new List<Order>
{
    new() { Id = 1, ProductId = 1, Quantity = 1, CustomerName = "John Doe", OrderDate = DateTime.Now.AddDays(-2) },
    new() { Id = 2, ProductId = 2, Quantity = 2, CustomerName = "Jane Smith", OrderDate = DateTime.Now.AddDays(-1) },
    new() { Id = 3, ProductId = 3, Quantity = 1, CustomerName = "Bob Johnson", OrderDate = DateTime.Now }
};

// Endpoints
app.MapGet("/orders", () => Results.Ok(orders))
    .WithName("GetAllOrders")
    .WithOpenApi();

app.MapGet("/orders/{id}", (int id) =>
    {
        var order = orders.FirstOrDefault(o => o.Id == id);
        return order != null ? Results.Ok(order) : Results.NotFound();
    })
    .WithName("GetOrderById")
    .WithOpenApi();

app.MapPost("/orders", (Order order) =>
    {
        order.Id = orders.Count + 1;
        order.OrderDate = DateTime.Now;
        orders.Add(order);
        return Results.Created($"/orders/{order.Id}", order);
    })
    .WithName("CreateOrder")
    .WithOpenApi();

app.MapGet("/", () => "Order Service is running!")
    .WithName("HealthCheck")
    .WithOpenApi();

// Service Discovery Registration
app.MapGet("/service-info", () => new
{
    ServiceName = "OrderService",
    Status = "Running",
    Port = app.Urls.FirstOrDefault()?.Split(':').Last() ?? "unknown",
    Endpoints = new[] { "/orders", "/orders/{id}", "POST /orders" }
});

app.Run();