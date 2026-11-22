using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Enums;
using CoffeeShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Services;

public class OrderService : IOrderService
{
    private readonly IDbContextFactory<CoffeeDbContext> _dbFactory;

    public OrderService(IDbContextFactory<CoffeeDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<ServiceResult<OrderDto>> CheckoutAsync(CheckoutRequest request)
    {
        if (request.Items.Count == 0 || request.Items.Any(i => i.Quantity <= 0))
            return ServiceResult<OrderDto>.Fail("Giỏ hàng trống hoặc có số lượng không hợp lệ.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId && u.IsActive);
        if (user is null)
            return ServiceResult<OrderDto>.Fail("Người dùng không hợp lệ hoặc đã bị khóa.");

        var activeSession = await db.WorkSessions
            .FirstOrDefaultAsync(ws => ws.UserId == request.UserId && ws.Status == WorkSessionStatus.Open);
        if (activeSession is null)
            return ServiceResult<OrderDto>.Fail("Bạn chưa vào ca làm việc! Vui lòng mở ca trước khi bán hàng.");

        var productIds = request.Items.Select(i => i.ProductId).Distinct().ToList();
        var products = await db.Products
            .Include(p => p.RecipeItems)
            .ThenInclude(r => r.Ingredient)
            .Where(p => productIds.Contains(p.Id) && !p.IsDeleted)
            .ToDictionaryAsync(p => p.Id);

        decimal subtotal = 0m;
        var mergedCart = request.Items
            .GroupBy(i => new { i.ProductId, Note = i.Note?.Trim() ?? string.Empty })
            .Select(g => new CartItemRequest(
                g.Key.ProductId,
                g.Sum(x => x.Quantity),
                string.IsNullOrEmpty(g.Key.Note) ? null : g.Key.Note))
            .ToList();

        var ingredientDemand = new Dictionary<int, decimal>();

        foreach (var item in mergedCart)
        {
            if (!products.TryGetValue(item.ProductId, out var product))
                return ServiceResult<OrderDto>.Fail($"Sản phẩm ID #{item.ProductId} không tồn tại.");

            if (!product.IsAvailable)
                return ServiceResult<OrderDto>.Fail($"Món '{product.Name}' hiện đang ngừng bán.");

            subtotal += product.Price * item.Quantity;

            foreach (var recipe in product.RecipeItems)
            {
                var need = recipe.QuantityRequired * item.Quantity;
                ingredientDemand[recipe.IngredientId] = ingredientDemand.GetValueOrDefault(recipe.IngredientId) + need;
            }
        }

        // Validate voucher server-side
        var voucherCode = request.VoucherCode?.Trim().ToUpperInvariant();
        decimal discountPercent = 0m;

        if (voucherCode == "ADMINVIP")
        {
            if (user.Role != UserRole.Admin)
                return ServiceResult<OrderDto>.Fail("Mã ADMINVIP chỉ dành cho Quản lý!");

            if (!request.CustomDiscountPercent.HasValue || request.CustomDiscountPercent.Value < 0m || request.CustomDiscountPercent.Value > 100m)
                return ServiceResult<OrderDto>.Fail("Phần trăm giảm giá ADMINVIP phải từ 0% đến 100%.");

            discountPercent = request.CustomDiscountPercent.Value;
        }
        else if (!string.IsNullOrEmpty(voucherCode))
        {
            var now = DateTime.Now;
            var voucher = await db.Vouchers.AsNoTracking()
                .FirstOrDefaultAsync(v => v.Code == voucherCode);

            if (voucher is null || !voucher.IsActive)
                return ServiceResult<OrderDto>.Fail($"Mã giảm giá '{voucherCode}' không tồn tại hoặc đã bị tắt.");

            if (voucher.ExpiresAt.HasValue && voucher.ExpiresAt.Value < now)
                return ServiceResult<OrderDto>.Fail($"Mã giảm giá '{voucherCode}' đã hết hạn.");

            if (subtotal < voucher.MinOrderAmount)
                return ServiceResult<OrderDto>.Fail($"Mã '{voucherCode}' chỉ áp dụng cho đơn từ {voucher.MinOrderAmount:N0} đ.");

            discountPercent = voucher.DiscountPercent;
        }
        else
        {
            voucherCode = null;
        }

        // Validate inventory sufficiency for ALL ingredients
        var requiredIngIds = ingredientDemand.Keys.ToList();
        var ingredients = await db.Ingredients
            .Where(i => requiredIngIds.Contains(i.Id))
            .ToDictionaryAsync(i => i.Id);

        foreach (var (ingId, neededQty) in ingredientDemand)
        {
            if (!ingredients.TryGetValue(ingId, out var ing))
                return ServiceResult<OrderDto>.Fail($"Nguyên liệu ID #{ingId} không tồn tại.");

            if (ing.Quantity < neededQty)
            {
                return ServiceResult<OrderDto>.Fail(
                    $"Không đủ nguyên liệu '{ing.Name}': cần {neededQty:N2} {ing.Unit}, tồn kho chỉ còn {ing.Quantity:N2} {ing.Unit}.");
            }
        }

        var discountAmount = Math.Round(subtotal * (discountPercent / 100m), 2, MidpointRounding.AwayFromZero);
        var finalAmount = Math.Max(0m, subtotal - discountAmount);
        var orderTime = DateTime.Now;
        var orderCode = $"ORD-{orderTime:yyyyMMddHHmmss}-{Guid.NewGuid().ToString("N")[..4].ToUpperInvariant()}";

        var order = new Order
        {
            OrderCode = orderCode,
            UserId = request.UserId,
            WorkSessionId = activeSession.Id,
            OrderDate = orderTime,
            Subtotal = subtotal,
            VoucherCode = voucherCode,
            DiscountPercent = discountPercent,
            DiscountAmount = discountAmount,
            FinalAmount = finalAmount,
            PaymentMethod = request.PaymentMethod,
            Status = OrderStatus.Paid
        };

        foreach (var item in mergedCart)
        {
            var product = products[item.ProductId];
            var unitCost = product.RecipeItems.Sum(r =>
                r.Ingredient is null ? 0m : Math.Round(r.QuantityRequired * r.Ingredient.AverageUnitCost, 2));

            order.OrderItems.Add(new OrderItem
            {
                ProductId = product.Id,
                ProductNameSnapshot = product.Name,
                Quantity = item.Quantity,
                UnitPrice = product.Price,
                UnitCost = unitCost,
                Note = item.Note
            });
        }

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        foreach (var (ingId, neededQty) in ingredientDemand)
        {
            var ing = ingredients[ingId];
            ing.Quantity -= neededQty;
            ing.UpdatedAt = orderTime;

            db.InventoryTransactions.Add(new InventoryTransaction
            {
                IngredientId = ing.Id,
                Type = InventoryTransactionType.SaleExport,
                Quantity = neededQty,
                UnitCost = ing.AverageUnitCost,
                TotalCost = Math.Round(neededQty * ing.AverageUnitCost, 2),
                Note = $"Bán đơn {order.OrderCode}",
                ReferenceOrderId = order.Id,
                UserId = request.UserId,
                CreatedAt = orderTime
            });
        }

        if (request.PaymentMethod == PaymentMethod.Cash)
            activeSession.CashSales += finalAmount;
        else
            activeSession.TransferSales += finalAmount;

        activeSession.TotalSales = activeSession.CashSales + activeSession.TransferSales;

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        var saved = await GetOrderDetailAsync(order.Id);
        return ServiceResult<OrderDto>.Ok(saved!, $"Thanh toán thành công đơn {order.OrderCode}!");
    }

    public async Task<IReadOnlyList<OrderDto>> GetOrdersAsync(OrderFilterRequest filter)
    {
        var startDt = filter.StartDate.ToDateTime(TimeOnly.MinValue);
        var endDt = filter.EndDate.ToDateTime(TimeOnly.MaxValue);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var query = db.Orders.AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.CancelledByUser)
            .Include(o => o.OrderItems)
            .Where(o => o.OrderDate >= startDt && o.OrderDate <= endDt);

        if (filter.StaffUserId.HasValue)
            query = query.Where(o => o.UserId == filter.StaffUserId.Value);

        if (filter.Status.HasValue)
            query = query.Where(o => o.Status == filter.Status.Value);

        var orders = await query.OrderByDescending(o => o.Id).ToListAsync();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var term = filter.Search.Trim();
            orders = orders.Where(o =>
                o.OrderCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                o.Id.ToString() == term ||
                (o.User is not null && o.User.FullName.Contains(term, StringComparison.OrdinalIgnoreCase)))
                .ToList();
        }

        return orders.Select(MapOrderDto).ToList();
    }

    public async Task<OrderDto?> GetOrderDetailAsync(int orderId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var order = await db.Orders.AsNoTracking()
            .Include(o => o.User)
            .Include(o => o.CancelledByUser)
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        return order is null ? null : MapOrderDto(order);
    }

    public async Task<ServiceResult> CancelOrderAsync(int orderId, int adminUserId, string reason)
    {
        if (string.IsNullOrWhiteSpace(reason))
            return ServiceResult.Fail("Vui lòng nhập lý do hủy đơn.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var adminUser = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == adminUserId && u.IsActive);
        if (adminUser is null || adminUser.Role != UserRole.Admin)
            return ServiceResult.Fail("Chỉ Quản lý (Admin) mới có quyền hủy đơn hàng.");

        var order = await db.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == orderId);

        if (order is null)
            return ServiceResult.Fail("Không tìm thấy đơn hàng.");

        if (order.Status == OrderStatus.Cancelled)
            return ServiceResult.Fail("Đơn hàng này đã bị hủy trước đó.");

        var now = DateTime.Now;
        var cleanReason = reason.Trim();

        order.Status = OrderStatus.Cancelled;
        order.CancelledByUserId = adminUserId;
        order.CancelledAt = now;
        order.CancelReason = cleanReason;

        var productIds = order.OrderItems.Select(oi => oi.ProductId).Distinct().ToList();
        var recipes = await db.RecipeItems
            .Where(r => productIds.Contains(r.ProductId))
            .ToListAsync();

        var restockByIng = new Dictionary<int, decimal>();
        foreach (var item in order.OrderItems)
        {
            foreach (var r in recipes.Where(x => x.ProductId == item.ProductId))
            {
                var amount = r.QuantityRequired * item.Quantity;
                restockByIng[r.IngredientId] = restockByIng.GetValueOrDefault(r.IngredientId) + amount;
            }
        }

        if (restockByIng.Count > 0)
        {
            var ingIds = restockByIng.Keys.ToList();
            var ingredients = await db.Ingredients
                .Where(i => ingIds.Contains(i.Id))
                .ToDictionaryAsync(i => i.Id);

            foreach (var (ingId, restockQty) in restockByIng)
            {
                if (!ingredients.TryGetValue(ingId, out var ing))
                    continue;

                // Restock quantity WITHOUT modifying AverageUnitCost
                ing.Quantity += restockQty;
                ing.UpdatedAt = now;

                db.InventoryTransactions.Add(new InventoryTransaction
                {
                    IngredientId = ing.Id,
                    Type = InventoryTransactionType.CancelRestock,
                    Quantity = restockQty,
                    TotalCost = 0m,
                    UnitCost = ing.AverageUnitCost,
                    Note = $"Hoàn kho hủy đơn {order.OrderCode}: {cleanReason}",
                    ReferenceOrderId = order.Id,
                    UserId = adminUserId,
                    CreatedAt = now
                });
            }
        }

        if (order.WorkSessionId.HasValue)
        {
            var session = await db.WorkSessions.FirstOrDefaultAsync(ws => ws.Id == order.WorkSessionId.Value);
            if (session is not null && session.Status == WorkSessionStatus.Open)
            {
                if (order.PaymentMethod == PaymentMethod.Cash)
                    session.CashSales = Math.Max(0m, session.CashSales - order.FinalAmount);
                else
                    session.TransferSales = Math.Max(0m, session.TransferSales - order.FinalAmount);

                session.TotalSales = session.CashSales + session.TransferSales;
            }
        }

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return ServiceResult.Ok($"Đã hủy đơn {order.OrderCode} và hoàn kho nguyên liệu!");
    }

    private static OrderDto MapOrderDto(Order o) => new(
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
            oi.Note)).ToList());
}
