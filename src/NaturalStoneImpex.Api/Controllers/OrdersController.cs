using Microsoft.AspNetCore.Authorization;
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

    [Authorize]
    [HttpGet]
    public async Task<IActionResult> GetAll([FromQuery] int? status, [FromQuery] int page = 1, [FromQuery] int pageSize = 20)
    {
        var result = await _orderService.GetAllAsync(status, page, pageSize);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("stats")]
    public async Task<IActionResult> GetStats()
    {
        var result = await _orderService.GetStatsAsync();
        return Ok(result);
    }

    [Authorize]
    [HttpGet("recent")]
    public async Task<IActionResult> GetRecent([FromQuery] int count = 5)
    {
        var result = await _orderService.GetRecentAsync(count);
        return Ok(result);
    }

    [Authorize]
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        try
        {
            var result = await _orderService.GetByIdAsync(id);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id}/confirm")]
    public async Task<IActionResult> Confirm(int id)
    {
        try
        {
            var result = await _orderService.ConfirmAsync(id);

            if (result is OrderConfirmErrorDto)
                return BadRequest(result);

            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id}/complete")]
    public async Task<IActionResult> Complete(int id)
    {
        try
        {
            await _orderService.CompleteAsync(id);
            return Ok(new { message = "Поръчката е маркирана като завършена." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id}/cancel")]
    public async Task<IActionResult> Cancel(int id)
    {
        try
        {
            await _orderService.CancelAsync(id);
            return Ok(new { message = "Поръчката е отказана." });
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [Authorize]
    [HttpPut("{id}/delivery-fee")]
    public async Task<IActionResult> SetDeliveryFee(int id, [FromBody] SetDeliveryFeeRequest request)
    {
        try
        {
            var result = await _orderService.SetDeliveryFeeAsync(id, request.DeliveryFee);
            return Ok(result);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { error = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }
}
