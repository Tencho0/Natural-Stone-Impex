using Microsoft.AspNetCore.Mvc;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Controllers;

[ApiController]
[Route("api/visualizer")]
public class VisualizerController : ControllerBase
{
    private readonly IProductService _productService;

    public VisualizerController(IProductService productService)
    {
        _productService = productService;
    }

    [HttpGet("products")]
    public async Task<IActionResult> GetProducts()
    {
        var products = await _productService.GetVisualizerProductsAsync();
        return Ok(products);
    }
}
