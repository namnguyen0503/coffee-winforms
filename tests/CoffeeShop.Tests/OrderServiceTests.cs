using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Tests;

public class OrderServiceTests
{
    [Fact]
    public async Task Checkout_ValidationFailures_EmptyCart_NoShift_InvalidProduct_UnavailableProduct()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var shiftSvc = fx.CreateShiftService();
        var menuSvc = fx.CreateMenuService();
        var orderSvc = fx.CreateOrderService();

        int staffId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;

        var black = (await menuSvc.GetProductsAsync()).Single(p => p.Name == "Cà phê Đen");

        // Empty or invalid qty
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(staffId, PaymentMethod.Cash, null, null, []))).Success);
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(black.Id, 0)]))).Success);

        // No open shift
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(black.Id, 1)]))).Success);

        // Invalid user
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(99999, PaymentMethod.Cash, null, null, [new CartItemRequest(black.Id, 1)]))).Success);

        await shiftSvc.StartShiftAsync(staffId, 100000m);

        // Nonexistent product
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(99999, 1)]))).Success);

        // Unavailable product
        await menuSvc.SaveProductAsync(new SaveProductRequest(
            black.Id, black.Name, black.Price, black.CategoryId, black.ImagePath, IsAvailable: false,
            black.Recipes.Select(r => new RecipeLineRequest(r.IngredientId, r.QuantityRequired)).ToList()));
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(black.Id, 1)]))).Success);
    }

    [Fact]
    public async Task Checkout_MergesIdenticalNoteLines_KeepsDifferentNotesSeparate_AndDeductsIngredients()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var shiftSvc = fx.CreateShiftService();
        var menuSvc = fx.CreateMenuService();
        var orderSvc = fx.CreateOrderService();

        int staffId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;

        await shiftSvc.StartShiftAsync(staffId, 500000m);
        var latte = (await menuSvc.GetProductsAsync()).Single(p => p.Name == "Latte Đá"); // 45,000, cost = 8,900
        var catId = (await menuSvc.GetCategoriesAsync()).First().Id;
        var water = (await menuSvc.SaveProductAsync(new SaveProductRequest(null, "Nước Suối", 10000m, catId, null, true, []))).Data!;

        var res = await orderSvc.CheckoutAsync(new CheckoutRequest(
            staffId,
            PaymentMethod.Transfer,
            "welcome",
            null,
            [
                new CartItemRequest(latte.Id, 1, " Ít đá "),
                new CartItemRequest(latte.Id, 2, "Ít đá"), // merges to qty 3
                new CartItemRequest(latte.Id, 1, "Nhiều sữa"), // separate line
                new CartItemRequest(water.Id, 1, null) // product with no recipe
            ]));

        Assert.True(res.Success);
        var order = res.Data!;
        Assert.Equal(190000m, order.Subtotal); // 4*45000 + 10000
        Assert.Equal(10m, order.DiscountPercent);
        Assert.Equal(19000m, order.DiscountAmount);
        Assert.Equal(171000m, order.FinalAmount);
        Assert.Equal(PaymentMethod.Transfer, order.PaymentMethod);
        Assert.Equal(3, order.Items.Count);
        Assert.Equal(3, order.Items.Single(i => i.ProductId == latte.Id && i.Note == "Ít đá").Quantity);
        Assert.Equal(8900m, order.Items.First(i => i.ProductId == latte.Id).UnitCost);

        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            var coffee = await db.Ingredients.SingleAsync(i => i.Name == "Hạt Cà phê");
            var milk = await db.Ingredients.SingleAsync(i => i.Name == "Sữa tươi");
            Assert.Equal(4920m, coffee.Quantity); // 5000 - 4*20
            Assert.Equal(19400m, milk.Quantity); // 20000 - 4*150
        }
    }

    [Fact]
    public async Task Checkout_VoucherServerSideValidation_RejectsMinOrderInactiveExpiredAndUnknown()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var shiftSvc = fx.CreateShiftService();
        var menuSvc = fx.CreateMenuService();
        var orderSvc = fx.CreateOrderService();
        var voucherSvc = fx.CreateVoucherService();

        int adminId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;

        await shiftSvc.StartShiftAsync(adminId, 200000m);
        var black = (await menuSvc.GetProductsAsync()).Single(p => p.Name == "Cà phê Đen"); // 25,000

        // VIP20 requires >= 50,000
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "VIP20", null, [new CartItemRequest(black.Id, 1)]))).Success);
        // 2 items = 50,000 -> VIP20 valid!
        Assert.True((await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "VIP20", null, [new CartItemRequest(black.Id, 2)]))).Success);

        // Inactive & expired & unknown vouchers rejected
        var exp = await voucherSvc.CreateAsync(new CreateVoucherRequest("EXPIRED", 15m, 0m, null, DateTime.Now.AddDays(-1)));
        var off = await voucherSvc.CreateAsync(new CreateVoucherRequest("OFFCODE", 15m, 0m, null, null));
        await voucherSvc.ToggleActiveAsync(off.Data!.Id);

        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "EXPIRED", null, [new CartItemRequest(black.Id, 1)]))).Success);
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "OFFCODE", null, [new CartItemRequest(black.Id, 1)]))).Success);
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "UNKNOWN99", null, [new CartItemRequest(black.Id, 1)]))).Success);
    }

    [Fact]
    public async Task Checkout_InsufficientAggregateStock_RollsBackAtomically()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var shiftSvc = fx.CreateShiftService();
        var menuSvc = fx.CreateMenuService();
        var orderSvc = fx.CreateOrderService();

        int adminId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            // Set Hạt Cà phê to 30g (enough for 1 coffee = 20g, NOT enough for 1 Black Coffee + 1 Latte = 40g)
            var coffee = await db.Ingredients.SingleAsync(i => i.Name == "Hạt Cà phê");
            coffee.Quantity = 30m;
            await db.SaveChangesAsync();
        }

        await shiftSvc.StartShiftAsync(adminId, 200000m);
        var products = await menuSvc.GetProductsAsync();
        var black = products.Single(p => p.Name == "Cà phê Đen");
        var latte = products.Single(p => p.Name == "Latte Đá");

        var failRes = await orderSvc.CheckoutAsync(new CheckoutRequest(
            adminId,
            PaymentMethod.Cash,
            null,
            null,
            [new CartItemRequest(black.Id, 1), new CartItemRequest(latte.Id, 1)]));

        Assert.False(failRes.Success);
        Assert.Contains("Hạt Cà phê", failRes.Message);

        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            var coffee = await db.Ingredients.SingleAsync(i => i.Name == "Hạt Cà phê");
            Assert.Equal(30m, coffee.Quantity);
            Assert.Equal(0, await db.Orders.CountAsync());
        }

        var preview = await shiftSvc.GetCurrentShiftPreviewAsync(adminId);
        Assert.Equal(0m, preview.Data!.TotalSales);
    }

    [Fact]
    public async Task AdminVipAndCancelOrder_RoleChecks_RestocksWithoutCorruptingWac_AndDoesNotMutateClosedShift()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var shiftSvc = fx.CreateShiftService();
        var menuSvc = fx.CreateMenuService();
        var orderSvc = fx.CreateOrderService();

        int adminId, staffId, whId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;
            whId = (await db.Users.SingleAsync(u => u.Username == "thukho")).Id;
        }

        var black = (await menuSvc.GetProductsAsync()).Single(p => p.Name == "Cà phê Đen");

        await shiftSvc.StartShiftAsync(staffId, 100000m);
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(staffId, PaymentMethod.Cash, "ADMINVIP", 50m, [new CartItemRequest(black.Id, 1)]))).Success);

        await shiftSvc.StartShiftAsync(adminId, 100000m);
        // Invalid ADMINVIP custom percents
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "ADMINVIP", null, [new CartItemRequest(black.Id, 1)]))).Success);
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "ADMINVIP", -5m, [new CartItemRequest(black.Id, 1)]))).Success);
        Assert.False((await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "ADMINVIP", 101m, [new CartItemRequest(black.Id, 1)]))).Success);

        var vip0 = await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Transfer, "ADMINVIP", 0m, [new CartItemRequest(black.Id, 1)]));
        var vip100 = await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, "ADMINVIP", 100m, [new CartItemRequest(black.Id, 3)]));
        Assert.True(vip0.Success && vip100.Success);
        Assert.Equal(25000m, vip0.Data!.FinalAmount);
        Assert.Equal(0m, vip100.Data!.FinalAmount);

        // Cancel validation: empty reason, non-admin roles, nonexistent order
        Assert.False((await orderSvc.CancelOrderAsync(vip100.Data.Id, adminId, "  ")).Success);
        Assert.False((await orderSvc.CancelOrderAsync(vip100.Data.Id, staffId, "Test")).Success);
        Assert.False((await orderSvc.CancelOrderAsync(vip100.Data.Id, whId, "Test")).Success);
        Assert.False((await orderSvc.CancelOrderAsync(99999, adminId, "Test")).Success);

        // Admin cancels vip100 -> restocks 60g coffee, keeps WAC 220đ, cannot cancel twice
        Assert.True((await orderSvc.CancelOrderAsync(vip100.Data.Id, adminId, "Khách đổi ý")).Success);
        Assert.False((await orderSvc.CancelOrderAsync(vip100.Data.Id, adminId, "Hủy lần 2")).Success);

        // Close shift with TransferSales = 25,000 stored; then cancel vip0 -> closed shift totals must stay historical
        var ended = await shiftSvc.EndShiftAsync(adminId, 100000m, null);
        Assert.Equal(25000m, ended.Data!.TransferSales);
        Assert.True((await orderSvc.CancelOrderAsync(vip0.Data.Id, adminId, "Hoàn tiền sau ca")).Success);

        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            var coffee = await db.Ingredients.SingleAsync(i => i.Name == "Hạt Cà phê");
            Assert.Equal(5000m, coffee.Quantity);
            Assert.Equal(220m, coffee.AverageUnitCost);

            var closedShift = await db.WorkSessions.SingleAsync(ws => ws.Id == ended.Data.Id);
            Assert.Equal(25000m, closedShift.TransferSales);

            var detail = await orderSvc.GetOrderDetailAsync(vip100.Data.Id);
            Assert.Equal(OrderStatus.Cancelled, detail!.Status);
            Assert.Equal("Khách đổi ý", detail.CancelReason);
        }
    }

    [Fact]
    public async Task GetOrdersAsync_FiltersByDateStaffStatusAndSearch()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var shiftSvc = fx.CreateShiftService();
        var menuSvc = fx.CreateMenuService();
        var orderSvc = fx.CreateOrderService();

        int adminId, staffId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;
        }

        var black = (await menuSvc.GetProductsAsync()).Single(p => p.Name == "Cà phê Đen");
        await shiftSvc.StartShiftAsync(staffId, 100000m);
        var oStaff = (await orderSvc.CheckoutAsync(new CheckoutRequest(staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(black.Id, 1)]))).Data!;

        await shiftSvc.StartShiftAsync(adminId, 100000m);
        var oAdmin = (await orderSvc.CheckoutAsync(new CheckoutRequest(adminId, PaymentMethod.Cash, null, null, [new CartItemRequest(black.Id, 1)]))).Data!;
        await orderSvc.CancelOrderAsync(oAdmin.Id, adminId, "Test filter");

        var today = DateOnly.FromDateTime(DateTime.Today);
        Assert.Equal(2, (await orderSvc.GetOrdersAsync(new OrderFilterRequest(today, today))).Count);
        Assert.Single(await orderSvc.GetOrdersAsync(new OrderFilterRequest(today, today, StaffUserId: staffId)));
        Assert.Single(await orderSvc.GetOrdersAsync(new OrderFilterRequest(today, today, Status: OrderStatus.Cancelled)));
        Assert.Single(await orderSvc.GetOrdersAsync(new OrderFilterRequest(today, today, Search: oStaff.OrderCode)));
        Assert.Single(await orderSvc.GetOrdersAsync(new OrderFilterRequest(today, today, Search: "Quản trị viên")));
        Assert.Empty(await orderSvc.GetOrdersAsync(new OrderFilterRequest(today.AddDays(-5), today.AddDays(-2))));
    }
}
