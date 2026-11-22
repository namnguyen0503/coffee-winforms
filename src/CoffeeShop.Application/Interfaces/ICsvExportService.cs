using CoffeeShop.Application.DTOs;

namespace CoffeeShop.Application.Interfaces;

public interface ICsvExportService
{
    void ExportOrders(string filePath, IReadOnlyList<OrderDto> orders);
    void ExportFinancialReport(string filePath, FinancialReportDto report);
    void ExportInventoryLogs(string filePath, IReadOnlyList<InventoryTransactionDto> logs);
    void ExportCashFlow(string filePath, IReadOnlyList<CashFlowRowDto> rows);
}
