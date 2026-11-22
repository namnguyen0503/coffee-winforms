using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;

namespace CoffeeShop.Application.Interfaces;

public interface IShiftService
{
    Task<WorkSessionDto?> GetActiveSessionAsync(int userId);
    Task<WorkSessionDto?> GetAnyActiveSessionAsync();
    Task<ServiceResult<WorkSessionDto>> StartShiftAsync(int userId, decimal startCash);
    Task<ServiceResult<EndShiftSummaryDto>> GetCurrentShiftPreviewAsync(int userId);
    Task<ServiceResult<WorkSessionDto>> EndShiftAsync(int userId, decimal endCash, string? note);
    Task<IReadOnlyList<CashFlowRowDto>> GetCashFlowAsync(DateOnly startDate, DateOnly endDate, bool diffOnly = false);
    Task<ServiceResult<CashTransactionDto>> WithdrawCashAsync(int adminUserId, AdminWithdrawCashRequest request);
    Task<IReadOnlyList<CashTransactionDto>> GetCashTransactionsAsync(DateOnly startDate, DateOnly endDate);
}
