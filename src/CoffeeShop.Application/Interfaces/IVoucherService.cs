using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Application.Interfaces;

public interface IVoucherService
{
    Task<IReadOnlyList<VoucherDto>> GetAllAsync();
    Task<VoucherValidationResult> ValidateAsync(string code, decimal orderSubtotal, UserRole userRole);
    Task<ServiceResult<VoucherDto>> CreateAsync(CreateVoucherRequest request);
    Task<ServiceResult> ToggleActiveAsync(int id);
    Task<ServiceResult> DeleteAsync(int id);
}
