using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Application.DTOs;

public record UserDto(
    int Id,
    string FullName,
    string Username,
    UserRole Role,
    bool IsActive,
    DateTime CreatedAt);

public record LoginResult(
    bool Success,
    string Message,
    UserDto? User = null);
