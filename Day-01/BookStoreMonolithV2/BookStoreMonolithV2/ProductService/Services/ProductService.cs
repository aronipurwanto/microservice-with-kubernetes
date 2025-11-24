using SharedModels.Models;
using System.Collections.Concurrent;

namespace ProductService.Services;

public interface IProductService
{
    Task<Product> CreateProductAsync(ProductCreateDto productCreateDto);
    Task<Product> GetProductAsync(Guid id);
    Task<List<Product>> GetAllProductsAsync();
    Task<List<Product>> GetProductsByCategoryAsync(Guid categoryId);
    Task<Product> UpdateProductAsync(Guid id, ProductCreateDto productUpdateDto);
    Task<bool> DeleteProductAsync(Guid id);
    Task<Category> CreateCategoryAsync(string name, string description);
    Task<List<Category>> GetAllCategoriesAsync();
}

public class ProductService : IProductService
{
    private static readonly ConcurrentDictionary<Guid, Product> _products = new();
    private static readonly ConcurrentDictionary<Guid, Category> _categories = new();

    static ProductService()
    {
        // Seed dummy categories
        var categories = new[]
        {
            new Category { Id = Guid.NewGuid(), Name = "Fiction", Description = "Fiction books" },
            new Category { Id = Guid.NewGuid(), Name = "Non-Fiction", Description = "Non-fiction books" },
            new Category { Id = Guid.NewGuid(), Name = "Technology", Description = "Technology and programming books" }
        };

        foreach (var category in categories)
        {
            _categories[category.Id] = category;
        }

        // Seed dummy products
        var products = new[]
        {
            new Product 
            { 
                Id = Guid.NewGuid(), 
                Name = "The Great Gatsby", 
                Description = "Classic novel by F. Scott Fitzgerald", 
                Price = 12.99m, 
                StockQuantity = 50,
                CategoryId = categories[0].Id,
                CreatedAt = DateTime.UtcNow
            },
            new Product 
            { 
                Id = Guid.NewGuid(), 
                Name = "Clean Code", 
                Description = "A Handbook of Agile Software Craftsmanship", 
                Price = 45.99m, 
                StockQuantity = 25,
                CategoryId = categories[2].Id,
                CreatedAt = DateTime.UtcNow
            },
            new Product 
            { 
                Id = Guid.NewGuid(), 
                Name = "Sapiens", 
                Description = "A Brief History of Humankind", 
                Price = 18.99m, 
                StockQuantity = 35,
                CategoryId = categories[1].Id,
                CreatedAt = DateTime.UtcNow
            }
        };

        foreach (var product in products)
        {
            _products[product.Id] = product;
            if (_categories.TryGetValue(product.CategoryId, out var category))
            {
                category.Products.Add(product);
            }
        }
    }

    public Task<Product> CreateProductAsync(ProductCreateDto productCreateDto)
    {
        if (!_categories.ContainsKey(productCreateDto.CategoryId))
        {
            throw new ArgumentException("Category not found");
        }

        var product = new Product
        {
            Id = Guid.NewGuid(),
            Name = productCreateDto.Name,
            Description = productCreateDto.Description,
            Price = productCreateDto.Price,
            StockQuantity = productCreateDto.StockQuantity,
            CategoryId = productCreateDto.CategoryId,
            CreatedAt = DateTime.UtcNow
        };

        _products[product.Id] = product;
        
        if (_categories.TryGetValue(product.CategoryId, out var category))
        {
            category.Products.Add(product);
        }

        return Task.FromResult(product);
    }

    public Task<Product> GetProductAsync(Guid id)
    {
        _products.TryGetValue(id, out var product);
        return Task.FromResult(product);
    }

    public Task<List<Product>> GetAllProductsAsync()
    {
        return Task.FromResult(_products.Values.ToList());
    }

    public Task<List<Product>> GetProductsByCategoryAsync(Guid categoryId)
    {
        var products = _products.Values
            .Where(p => p.CategoryId == categoryId)
            .ToList();
        return Task.FromResult(products);
    }

    public Task<Product> UpdateProductAsync(Guid id, ProductCreateDto productUpdateDto)
    {
        if (_products.TryGetValue(id, out var product))
        {
            product.Name = productUpdateDto.Name;
            product.Description = productUpdateDto.Description;
            product.Price = productUpdateDto.Price;
            product.StockQuantity = productUpdateDto.StockQuantity;
            
            if (product.CategoryId != productUpdateDto.CategoryId)
            {
                // Remove from old category
                if (_categories.TryGetValue(product.CategoryId, out var oldCategory))
                {
                    oldCategory.Products.RemoveAll(p => p.Id == id);
                }
                
                // Add to new category
                product.CategoryId = productUpdateDto.CategoryId;
                if (_categories.TryGetValue(product.CategoryId, out var newCategory))
                {
                    newCategory.Products.Add(product);
                }
            }

            _products[id] = product;
            return Task.FromResult(product);
        }
        return Task.FromResult<Product>(null);
    }

    public Task<bool> DeleteProductAsync(Guid id)
    {
        if (_products.TryRemove(id, out var product))
        {
            if (_categories.TryGetValue(product.CategoryId, out var category))
            {
                category.Products.RemoveAll(p => p.Id == id);
            }
            return Task.FromResult(true);
        }
        return Task.FromResult(false);
    }

    public Task<Category> CreateCategoryAsync(string name, string description)
    {
        var category = new Category
        {
            Id = Guid.NewGuid(),
            Name = name,
            Description = description
        };

        _categories[category.Id] = category;
        return Task.FromResult(category);
    }

    public Task<List<Category>> GetAllCategoriesAsync()
    {
        return Task.FromResult(_categories.Values.ToList());
    }
}