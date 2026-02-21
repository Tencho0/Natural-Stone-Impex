using NaturalStoneImpex.Api.Models.DTOs;

namespace NaturalStoneImpex.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
}
