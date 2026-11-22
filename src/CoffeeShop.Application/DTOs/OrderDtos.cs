using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Application.DTOs;

public record CartItemRequest(
    int ProductId,
    int Quantity,
    string? Note = null);

public record CheckoutRequest(
    int UserId,
    PaymentMethod PaymentMethod,
    string? VoucherCode,
    decimal? CustomDiscountPercent,
    IReadOnlyList<CartItemRequest> Items);

public record OrderItemDto(
    int ProductId,
    string ProductName,
    int Quantity,
    decimal UnitPrice,
    decimal UnitCost,
    string? Note)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

public record OrderDto(
    int Id,
    string OrderCode,
    int UserId,
    string StaffName,
    int? WorkSessionId,
    DateTime OrderDate,
    decimal Subtotal,
    string? VoucherCode,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal FinalAmount,
    PaymentMethod PaymentMethod,
    OrderStatus Status,
    string? CancelledByName,
    DateTime? CancelledAt,
    string? CancelReason,
    IReadOnlyList<OrderItemDto> Items);

public record OrderFilterRequest(
    DateOnly StartDate,
    DateOnly EndDate,
    string? Search = null,
    int? StaffUserId = null,
    OrderStatus? Status = null);
