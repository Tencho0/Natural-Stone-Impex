using Microsoft.AspNetCore.Mvc;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Controllers;

[ApiController]
[Route("api/orders")]
public class OrdersController : ControllerBase
{
    private readonly IOrderService _orderService;

    public OrdersController(IOrderService orderService)
    {
        _orderService = orderService;
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateOrderRequest request)
    {
        try
        {
            var response = await _orderService.CreateAsync(request);
            return StatusCode(201, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
