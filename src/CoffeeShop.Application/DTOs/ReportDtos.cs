namespace CoffeeShop.Application.DTOs;

public record OrderProfitRowDto(
    int OrderId,
    string OrderCode,
    DateTime OrderDate,
    string StaffName,
    string PaymentMethodLabel,
    decimal Revenue,
    decimal CostOfGoodsSold,
    decimal GrossProfit);

public record TopProductRowDto(
    int ProductId,
    string ProductName,
    int QuantitySold,
    decimal Revenue,
    decimal TotalCost,
    decimal GrossProfit);

public record FinancialReportDto(
    decimal TotalRevenue,
    decimal TotalCogs,
    decimal GrossProfit,
    decimal GrossMarginPercent,
    int TotalOrders,
    decimal CashRevenue,
    decimal TransferRevenue,
    IReadOnlyList<OrderProfitRowDto> Orders,
    IReadOnlyList<TopProductRowDto> TopProducts);

public record DashboardSummaryDto(
    int TodayOrderCount,
    decimal TodayRevenue,
    int LowStockCount,
    IReadOnlyList<IngredientDto> LowStockIngredients,
    IReadOnlyList<OrderDto> RecentOrders);
