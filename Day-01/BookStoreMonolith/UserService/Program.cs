using Microsoft.EntityFrameworkCore;
using UserService.Models;
using UserService.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using SharedModels.Models;
using UserService.Controllers;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Configure Entity Framework with SQL Server - FIXED
builder.Services.AddDbContext<UserContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Register services
builder.Services.AddScoped<IUserService, UserService.Services.UserService>();

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddCheck<UserContextHealthCheck>("UserContext");

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
    var context = scope.ServiceProvider.GetRequiredService<UserContext>();
    context.Database.EnsureCreated();
    
    // Seed sample data if needed
    await SeedSampleData(context);
}

app.Run();

// Seed sample data method
static async Task SeedSampleData(UserContext context)
{
    if (!context.Users.Any())
    {
        var users = new[]
        {
            new User 
            { 
                Id = Guid.NewGuid(), 
                Email = "admin@bookstore.com", 
                FirstName = "System", 
                LastName = "Administrator",
                CreatedAt = DateTime.UtcNow
            },
            new User 
            { 
                Id = Guid.NewGuid(), 
                Email = "john.doe@example.com", 
                FirstName = "John", 
                LastName = "Doe",
                CreatedAt = DateTime.UtcNow
            },
            new User 
            { 
                Id = Guid.NewGuid(), 
                Email = "jane.smith@example.com", 
                FirstName = "Jane", 
                LastName = "Smith",
                CreatedAt = DateTime.UtcNow
            }
        };

        await context.Users.AddRangeAsync(users);
        await context.SaveChangesAsync();
    }
}