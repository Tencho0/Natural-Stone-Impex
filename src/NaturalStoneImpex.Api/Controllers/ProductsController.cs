using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Controllers;

[ApiController]
[Route("api/products")]
public class ProductsController : ControllerBase
{
    private readonly IProductService _productService;

    public ProductsController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll(
        [FromQuery] int? categoryId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 12)
    {
        var includeInactive = User.Identity?.IsAuthenticated == true;
        var result = await _productService.GetAllAsync(categoryId, search, page, pageSize, includeInactive);
        return Ok(result);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var product = await _productService.GetByIdAsync(id);
        if (product is null)
            return NotFound(new { error = "Продуктът не е намерен." });

        return Ok(product);
    }

    [Authorize]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProductRequest request)
    {
        try
        {
            var product = await _productService.CreateAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = product.Id }, product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProductRequest request)
    {
        try
        {
            var product = await _productService.UpdateAsync(id, request);
            if (product is null)
                return NotFound(new { error = "Продуктът не е намерен." });

            return Ok(product);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var (success, error) = await _productService.DeleteAsync(id);

        if (!success)
            return NotFound(new { error });

        return NoContent();
    }

    [Authorize]
    [HttpPost("{id}/image")]
    public async Task<IActionResult> UploadImage(int id, IFormFile image)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = "Файлът е задължителен." });

        var (imagePath, error) = await _productService.UploadImageAsync(id, image);

        if (error == "Продуктът не е намерен.")
            return NotFound(new { error });

        if (error is not null)
            return BadRequest(new { error });

        return Ok(new { imagePath });
    }

    [Authorize]
    [HttpGet("low-stock")]
    public async Task<IActionResult> GetLowStock([FromQuery] decimal threshold = 10)
    {
        var products = await _productService.GetLowStockAsync(threshold);
        return Ok(products);
    }
}
