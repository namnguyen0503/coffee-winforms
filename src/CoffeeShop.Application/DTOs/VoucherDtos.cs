namespace CoffeeShop.Application.DTOs;

public record VoucherDto(
    int Id,
    string Code,
    decimal DiscountPercent,
    decimal MinOrderAmount,
    string? Description,
    bool IsActive,
    DateTime? ExpiresAt,
    DateTime CreatedAt);

public record VoucherValidationResult(
    bool IsValid,
    string Code,
    decimal DiscountPercent,
    string Message,
    bool RequiresAdminCustomPercent = false);

public record CreateVoucherRequest(
    string Code,
    decimal DiscountPercent,
    decimal MinOrderAmount,
    string? Description,
    DateTime? ExpiresAt);
