using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Services;

public class ReportService : IReportService
{
    private readonly IDbContextFactory<CoffeeDbContext> _dbFactory;

    public ReportService(IDbContextFactory<CoffeeDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<DashboardSummaryDto> GetDashboardSummaryAsync(int? staffUserId = null)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var startDt = today.ToDateTime(TimeOnly.MinValue);
        var endDt = today.ToDateTime(TimeOnly.MaxValue);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var todayOrdersQuery = db.Orders.AsNoTracking()
            .Where(o => o.Status == OrderStatus.Paid && o.OrderDate >= startDt && o.OrderDate <= endDt);

        if (staffUserId.HasValue)
            todayOrdersQuery = todayOrdersQuery.Where(o => o.UserId == staffUserId.Value);

        var todayPaidAmounts = await todayOrdersQuery.Select(o => o.FinalAmount).ToListAsync();

        var lowStockIngredients = (await db.Ingredients.AsNoTracking()
            .Where(i => i.Quantity <= i.MinQuantity)
            .Select(i => new IngredientDto(
                i.Id,
                i.Name,
                i.Unit,
                i.Quantity,
                i.MinQuantity,
                i.AverageUnitCost,
                i.UpdatedAt))
            .ToListAsync())
            .OrderBy(i => i.Quantity)
            .ToList();

        var recentQuery = db.Orders.AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.CancelledByUser)
            .Include(o => o.OrderItems)
            .AsQueryable();

        if (staffUserId.HasValue)
            recentQuery = recentQuery.Where(o => o.UserId == staffUserId.Value);

        var recentEntities = await recentQuery
            .OrderByDescending(o => o.Id)
            .Take(8)
            .ToListAsync();

        var recentOrders = recentEntities.Select(o => new OrderDto(
            o.Id,
            o.OrderCode,
            o.UserId,
            o.User?.FullName ?? "N/A",
            o.WorkSessionId,
            o.OrderDate,
            o.Subtotal,
            o.VoucherCode,
            o.DiscountPercent,
            o.DiscountAmount,
            o.FinalAmount,
            o.PaymentMethod,
            o.Status,
            o.CancelledByUser?.FullName,
            o.CancelledAt,
            o.CancelReason,
            o.OrderItems.Select(oi => new OrderItemDto(
                oi.ProductId,
                oi.ProductNameSnapshot,
                oi.Quantity,
                oi.UnitPrice,
                oi.UnitCost,
                oi.Note)).ToList())).ToList();

        return new DashboardSummaryDto(
            todayPaidAmounts.Count,
            todayPaidAmounts.Sum(),
            lowStockIngredients.Count,
            lowStockIngredients,
            recentOrders);
    }

    public async Task<FinancialReportDto> GetFinancialReportAsync(DateOnly startDate, DateOnly endDate)
    {
        var startDt = startDate.ToDateTime(TimeOnly.MinValue);
        var endDt = endDate.ToDateTime(TimeOnly.MaxValue);

        await using var db = await _dbFactory.CreateDbContextAsync();

        var paidOrders = await db.Orders.AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.OrderItems)
            .Where(o => o.Status == OrderStatus.Paid && o.OrderDate >= startDt && o.OrderDate <= endDt)
            .OrderByDescending(o => o.OrderDate)
            .ThenByDescending(o => o.Id)
            .ToListAsync();

        decimal totalRevenue = 0m;
        decimal totalCogs = 0m;
        decimal cashRevenue = 0m;
        decimal transferRevenue = 0m;
        var orderRows = new List<OrderProfitRowDto>(paidOrders.Count);

        var productAgg = new Dictionary<int, (string Name, int Qty, decimal GrossRev, decimal DiscountedRev, decimal Cost)>();

        foreach (var order in paidOrders)
        {
            var revenue = order.FinalAmount;
            var orderCogs = order.OrderItems.Sum(oi => oi.UnitCost * oi.Quantity);
            var orderProfit = revenue - orderCogs;

            totalRevenue += revenue;
            totalCogs += orderCogs;
            if (order.PaymentMethod == PaymentMethod.Cash)
                cashRevenue += revenue;
            else
                transferRevenue += revenue;

            orderRows.Add(new OrderProfitRowDto(
                order.Id,
                order.OrderCode,
                order.OrderDate,
                order.User?.FullName ?? "N/A",
                order.PaymentMethod == PaymentMethod.Cash ? "Tiền mặt" : "Chuyển khoản",
                revenue,
                orderCogs,
                orderProfit));

            // Attribute order discount proportionally across items so sum of top-product revenues matches totalRevenue
            var discountFactor = order.Subtotal > 0 ? order.FinalAmount / order.Subtotal : 1m;
            foreach (var item in order.OrderItems)
            {
                var itemRev = Math.Round(item.UnitPrice * item.Quantity * discountFactor, 2);
                var itemCost = Math.Round(item.UnitCost * item.Quantity, 2);
                if (productAgg.TryGetValue(item.ProductId, out var acc))
                {
                    productAgg[item.ProductId] = (
                        item.ProductNameSnapshot,
                        acc.Qty + item.Quantity,
                        acc.GrossRev + (item.UnitPrice * item.Quantity),
                        acc.DiscountedRev + itemRev,
                        acc.Cost + itemCost);
                }
                else
                {
                    productAgg[item.ProductId] = (
                        item.ProductNameSnapshot,
                        item.Quantity,
                        item.UnitPrice * item.Quantity,
                        itemRev,
                        itemCost);
                }
            }
        }

        var grossProfit = totalRevenue - totalCogs;
        var marginPct = totalRevenue > 0 ? Math.Round((grossProfit / totalRevenue) * 100m, 2) : 0m;

        var topProducts = productAgg
            .Select(kvp => new TopProductRowDto(
                kvp.Key,
                kvp.Value.Name,
                kvp.Value.Qty,
                kvp.Value.DiscountedRev,
                kvp.Value.Cost,
                kvp.Value.DiscountedRev - kvp.Value.Cost))
            .OrderByDescending(p => p.QuantitySold)
            .ThenByDescending(p => p.Revenue)
            .ToList();

        return new FinancialReportDto(
            totalRevenue,
            totalCogs,
            grossProfit,
            marginPct,
            paidOrders.Count,
            cashRevenue,
            transferRevenue,
            orderRows,
            topProducts);
    }
}
