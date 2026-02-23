namespace NaturalStoneImpex.Client.Services;

public interface IAuthService
{
    Task<string?> LoginAsync(string username, string password);
    void Logout();
    bool IsLoggedIn { get; }
}
