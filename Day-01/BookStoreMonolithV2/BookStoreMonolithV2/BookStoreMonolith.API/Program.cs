using OrderService.Services;
using PaymentService.Services;
using UserService.Services;
using ProductService.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register services
builder.Services.AddScoped<IOrderService, OrderService.Services.OrderService>();
builder.Services.AddScoped<IPaymentService, PaymentService.Services.PaymentService>();
builder.Services.AddScoped<IUserService, UserService.Services.UserService>();
builder.Services.AddScoped<IProductService, ProductService.Services.ProductService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseAuthorization();
app.MapControllers();

app.Run();