using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NaturalStoneImpex.Api.Auth;
using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Services;

namespace NaturalStoneImpex.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginRequest request)
    {
        var response = await _authService.LoginAsync(request);

        if (response is null)
            return Unauthorized(new { error = "Невалидно потребителско име или парола." });

        return Ok(response);
    }

    [Authorize]
    [HttpGet("me")]
    public IActionResult Me()
        => Ok(new { username = User.FindFirst("username")?.Value ?? User.Identity!.Name });

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(AuthSchemes.Cookie);
        return NoContent();
    }
}
