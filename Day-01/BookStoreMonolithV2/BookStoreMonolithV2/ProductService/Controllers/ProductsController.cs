using Microsoft.AspNetCore.Mvc;
using ProductService.Services;
using SharedModels.Models;

namespace ProductService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpPost]
    public async Task<ActionResult<Product>> CreateProduct(ProductCreateDto productCreateDto)
    {
        try
        {
            var product = await _productService.CreateProductAsync(productCreateDto);
            return CreatedAtAction(nameof(GetProduct), new { id = product.Id }, product);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(ex.Message);
        }
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<Product>> GetProduct(Guid id)
    {
        var product = await _productService.GetProductAsync(id);
        if (product == null)
        {
            return NotFound();
        }
        return product;
    }

    [HttpGet]
    public async Task<ActionResult<List<Product>>> GetAllProducts()
    {
        var products = await _productService.GetAllProductsAsync();
        return products;
    }

    [HttpGet("category/{categoryId}")]
    public async Task<ActionResult<List<Product>>> GetProductsByCategory(Guid categoryId)
    {
        var products = await _productService.GetProductsByCategoryAsync(categoryId);
        return products;
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<Product>> UpdateProduct(Guid id, ProductCreateDto productUpdateDto)
    {
        var product = await _productService.UpdateProductAsync(id, productUpdateDto);
        if (product == null)
        {
            return NotFound();
        }
        return product;
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteProduct(Guid id)
    {
        var result = await _productService.DeleteProductAsync(id);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("categories")]
    public async Task<ActionResult<Category>> CreateCategory(string name, string description)
    {
        var category = await _productService.CreateCategoryAsync(name, description);
        return category;
    }

    [HttpGet("categories")]
    public async Task<ActionResult<List<Category>>> GetAllCategories()
    {
        var categories = await _productService.GetAllCategoriesAsync();
        return categories;
    }
}