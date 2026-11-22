using CoffeeShop.Application.DTOs;

namespace CoffeeShop.Application.Interfaces;

public interface IAuthService
{
    Task<LoginResult> LoginAsync(string username, string password, DateTime? currentTime = null);
}
