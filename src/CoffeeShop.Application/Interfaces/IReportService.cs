using CoffeeShop.Application.DTOs;

namespace CoffeeShop.Application.Interfaces;

public interface IReportService
{
    Task<DashboardSummaryDto> GetDashboardSummaryAsync(int? staffUserId = null);
    Task<FinancialReportDto> GetFinancialReportAsync(DateOnly startDate, DateOnly endDate);
}
