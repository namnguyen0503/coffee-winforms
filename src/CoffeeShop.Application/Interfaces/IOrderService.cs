using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;

namespace CoffeeShop.Application.Interfaces;

public interface IOrderService
{
    Task<ServiceResult<OrderDto>> CheckoutAsync(CheckoutRequest request);
    Task<IReadOnlyList<OrderDto>> GetOrdersAsync(OrderFilterRequest filter);
    Task<OrderDto?> GetOrderDetailAsync(int orderId);
    Task<ServiceResult> CancelOrderAsync(int orderId, int adminUserId, string reason);
}
