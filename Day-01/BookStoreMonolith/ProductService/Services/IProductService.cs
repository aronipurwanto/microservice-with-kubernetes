using Microsoft.EntityFrameworkCore;
using ProductService.Models;
using SharedModels.Models;

namespace ProductService.Services;

public interface IProductService
{
    Task<List<Product>> GetProductsAsync();
    Task<Product?> GetProductAsync(Guid id);
    Task<Product> CreateProductAsync(ProductCreateDto productDto);
    Task<bool> UpdateProductAsync(Guid id, ProductCreateDto productDto);
    Task<bool> DeleteProductAsync(Guid id);
    Task<List<Category>> GetCategoriesAsync();
}

public class ProductService : IProductService
{
    private readonly ProductContext _context;
    private readonly ILogger<ProductService> _logger;

    public ProductService(ProductContext context, ILogger<ProductService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task<List<Product>> GetProductsAsync()
    {
        return await _context.Products
            .Include(p => p.Category)
            .ToListAsync();
    }

    public async Task<Product?> GetProductAsync(Guid id)
    {
        return await _context.Products
            .Include(p => p.Category)
            .FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<Product> CreateProductAsync(ProductCreateDto productDto)
    {
        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = productDto.Name,
            Description = productDto.Description,
            Price = productDto.Price,
            StockQuantity = productDto.StockQuantity,
            CategoryId = productDto.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        _context.Products.Add(product);
        await _context.SaveChangesAsync();

        _logger.LogInformation("Created product with ID: {ProductId}", product.Id);
        return product;
    }

    public async Task<bool> UpdateProductAsync(Guid id, ProductCreateDto productDto)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return false;

        product.Name = productDto.Name;
        product.Description = productDto.Description;
        product.Price = productDto.Price;
        product.StockQuantity = productDto.StockQuantity;
        product.CategoryId = productDto.CategoryId;

        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<bool> DeleteProductAsync(Guid id)
    {
        var product = await _context.Products.FindAsync(id);
        if (product == null) return false;

        _context.Products.Remove(product);
        await _context.SaveChangesAsync();
        return true;
    }

    public async Task<List<Category>> GetCategoriesAsync()
    {
        return await _context.Categories.ToListAsync();
    }
}