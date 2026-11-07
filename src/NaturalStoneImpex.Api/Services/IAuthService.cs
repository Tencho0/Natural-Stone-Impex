using NaturalStoneImpex.Api.Models.DTOs;
using NaturalStoneImpex.Api.Models.Entities;

namespace NaturalStoneImpex.Api.Services;

public interface IAuthService
{
    Task<LoginResponse?> LoginAsync(LoginRequest request);
    Task<AdminUser?> ValidateCredentialsAsync(string username, string password);
}
