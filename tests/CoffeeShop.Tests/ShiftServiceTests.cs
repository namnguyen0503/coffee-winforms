using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Tests;

public class ShiftServiceTests
{
    [Fact]
    public async Task StartShift_ValidationAndDuplicatePrevention_Works()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var shiftSvc = fx.CreateShiftService();
        var userSvc = fx.CreateUserScheduleService();

        int staffId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;

        Assert.False((await shiftSvc.StartShiftAsync(staffId, -100m)).Success);
        Assert.False((await shiftSvc.StartShiftAsync(99999, 500000m)).Success);

        var lockedUser = await userSvc.SaveUserAsync(new SaveUserRequest(null, "Locked", "lockshift", "123456", UserRole.Staff, false));
        Assert.False((await shiftSvc.StartShiftAsync(lockedUser.Data!.Id, 500000m)).Success);

        var ok = await shiftSvc.StartShiftAsync(staffId, 0m);
        Assert.True(ok.Success);
        Assert.NotNull(await shiftSvc.GetActiveSessionAsync(staffId));
        Assert.Equal(ok.Data!.Id, (await shiftSvc.GetAnyActiveSessionAsync())!.Id);

        // Cannot open a second shift while own shift is open
        var dup = await shiftSvc.StartShiftAsync(staffId, 100000m);
        Assert.False(dup.Success);
    }

    [Fact]
    public async Task StartShift_AndEndShift_CalculatesCashTransferAndVarianceAccurately_AndExcludesCancelled()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var shiftSvc = fx.CreateShiftService();
        var orderSvc = fx.CreateOrderService();
        var menuSvc = fx.CreateMenuService();

        int adminId, staffId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;
        }

        // Preview / end before start fails
        Assert.False((await shiftSvc.GetCurrentShiftPreviewAsync(staffId)).Success);
        Assert.False((await shiftSvc.EndShiftAsync(staffId, 100000m, null)).Success);

        var startRes = await shiftSvc.StartShiftAsync(staffId, 500000m);
        Assert.True(startRes.Success);

        var products = await menuSvc.GetProductsAsync();
        var blackCoffee = products.Single(p => p.Name == "Cà phê Đen"); // 25,000
        var latte = products.Single(p => p.Name == "Latte Đá"); // 45,000

        // 1 cash order = 50,000; 1 transfer order = 45,000; 1 cancelled cash order = 25,000
        var cashOrder = await orderSvc.CheckoutAsync(new CheckoutRequest(
            staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(blackCoffee.Id, 2)]));
        var transferOrder = await orderSvc.CheckoutAsync(new CheckoutRequest(
            staffId, PaymentMethod.Transfer, null, null, [new CartItemRequest(latte.Id, 1)]));
        var cancelledOrder = await orderSvc.CheckoutAsync(new CheckoutRequest(
            staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(blackCoffee.Id, 1)]));

        Assert.True(cashOrder.Success && transferOrder.Success && cancelledOrder.Success);
        Assert.True((await orderSvc.CancelOrderAsync(cancelledOrder.Data!.Id, adminId, "Bấm nhầm")).Success);

        var preview = await shiftSvc.GetCurrentShiftPreviewAsync(staffId);
        Assert.True(preview.Success);
        Assert.Equal(50000m, preview.Data!.CashSales);
        Assert.Equal(45000m, preview.Data.TransferSales);
        Assert.Equal(95000m, preview.Data.TotalSales);
        Assert.Equal(550000m, preview.Data.ExpectedCash);
        Assert.Equal(2, preview.Data.OrderCount);

        // Negative endCash rejected
        Assert.False((await shiftSvc.EndShiftAsync(staffId, -1m, null)).Success);

        // Counted 540,000 -> variance -10,000
        var endRes = await shiftSvc.EndShiftAsync(staffId, 540000m, "  Thiếu 10k tiền lẻ  ");
        Assert.True(endRes.Success);
        Assert.Equal(WorkSessionStatus.Closed, endRes.Data!.Status);
        Assert.Equal(550000m, endRes.Data.ExpectedCash);
        Assert.Equal(-10000m, endRes.Data.CashVariance);
        Assert.Equal("Thiếu 10k tiền lẻ", endRes.Data.Note);
    }

    [Fact]
    public async Task EndShift_ExactAndSurplusCases_CalculateCorrectVariance()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var shiftSvc = fx.CreateShiftService();

        int adminId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;

        await shiftSvc.StartShiftAsync(adminId, 200000m);
        var exact = await shiftSvc.EndShiftAsync(adminId, 200000m, "   ");
        Assert.True(exact.Success);
        Assert.Equal(0m, exact.Data!.CashVariance);
        Assert.Null(exact.Data.Note);

        await shiftSvc.StartShiftAsync(adminId, 200000m);
        var surplus = await shiftSvc.EndShiftAsync(adminId, 215000m, "Khách bo");
        Assert.True(surplus.Success);
        Assert.Equal(15000m, surplus.Data!.CashVariance);
    }

    [Fact]
    public async Task StartShift_ForceClosesOtherUsersAbandonedShift_AndGetCashFlowFiltersCorrectly()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();

        var shiftSvc = fx.CreateShiftService();
        var orderSvc = fx.CreateOrderService();
        var menuSvc = fx.CreateMenuService();

        int adminId, staffId;
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            adminId = (await db.Users.SingleAsync(u => u.Username == "admin")).Id;
            staffId = (await db.Users.SingleAsync(u => u.Username == "staff")).Id;
        }

        // Shift 1: Admin opens and closes cleanly with 300k
        await shiftSvc.StartShiftAsync(adminId, 300000m);
        await shiftSvc.EndShiftAsync(adminId, 300000m, null);

        // Shift 2: Staff opens with 300k (handover diff = 0), sells 25k cash, forgets to close
        await shiftSvc.StartShiftAsync(staffId, 300000m);
        var blackCoffee = (await menuSvc.GetProductsAsync()).Single(p => p.Name == "Cà phê Đen");
        await orderSvc.CheckoutAsync(new CheckoutRequest(
            staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(blackCoffee.Id, 1)]));

        // Shift 3: Admin opens shift with 320k -> staff's shift force-closed (expected=325k, end=320k, variance=-5k)
        var adminStart = await shiftSvc.StartShiftAsync(adminId, 320000m);
        Assert.True(adminStart.Success);
        Assert.Null(await shiftSvc.GetActiveSessionAsync(staffId));

        var today = DateOnly.FromDateTime(DateTime.Today);
        var allFlow = await shiftSvc.GetCashFlowAsync(today, today, diffOnly: false);
        Assert.Equal(3, allFlow.Count);

        var closedStaffShift = allFlow.Single(r => r.StaffName == "Nhân viên Thu ngân");
        Assert.Equal(25000m, closedStaffShift.CashSales);
        Assert.Equal(325000m, closedStaffShift.ExpectedCash);
        Assert.Equal(320000m, closedStaffShift.EndCash);
        Assert.Equal(-5000m, closedStaffShift.CashVariance);
        Assert.Equal(0m, allFlow.First().HandoverDiff); // 320k - 320k = 0

        // diffOnly should include ONLY the shift with variance (-5k) 
        var diffOnlyFlow = await shiftSvc.GetCashFlowAsync(today, today, diffOnly: true);
        Assert.Single(diffOnlyFlow);
        Assert.Equal(closedStaffShift.SessionId, diffOnlyFlow[0].SessionId);

        // Out-of-range date filter returns empty
        var emptyFlow = await shiftSvc.GetCashFlowAsync(today.AddDays(-10), today.AddDays(-5));
        Assert.Empty(emptyFlow);
    }

    [Fact]
    public async Task AdminWithdrawCash_ValidatesRoleBalanceReason_AndUpdatesShiftAndHandoverCashFlow()
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

        // Reject invalid amount, empty reason, non-admin, and empty register
        Assert.False((await shiftSvc.WithdrawCashAsync(adminId, new AdminWithdrawCashRequest(0m, "Test"))).Success);
        Assert.False((await shiftSvc.WithdrawCashAsync(adminId, new AdminWithdrawCashRequest(10000m, "   "))).Success);
        Assert.False((await shiftSvc.WithdrawCashAsync(staffId, new AdminWithdrawCashRequest(10000m, "Lý do"))).Success);
        Assert.False((await shiftSvc.WithdrawCashAsync(adminId, new AdminWithdrawCashRequest(10000m, "Chưa có ca"))).Success);

        // Staff starts shift with 0 starting cash, sells 2 black coffees (50,000 Cash)
        var s1 = await shiftSvc.StartShiftAsync(staffId, 0m);
        Assert.True(s1.Success);
        var coffee = (await menuSvc.GetProductsAsync()).Single(p => p.Name == "Cà phê Đen");
        Assert.True((await orderSvc.CheckoutAsync(new CheckoutRequest(
            staffId, PaymentMethod.Cash, null, null, [new CartItemRequest(coffee.Id, 2)]))).Success);

        // Exceeding balance (50k) fails; valid 20k withdrawal succeeds and updates expected cash to 30k
        Assert.False((await shiftSvc.WithdrawCashAsync(adminId, new AdminWithdrawCashRequest(60000m, "Quá két"))).Success);
        var w1 = await shiftSvc.WithdrawCashAsync(adminId, new AdminWithdrawCashRequest(20000m, "Mua đá viên"));
        Assert.True(w1.Success);
        Assert.Equal(s1.Data!.Id, w1.Data!.WorkSessionId);

        var preview = await shiftSvc.GetCurrentShiftPreviewAsync(staffId);
        Assert.True(preview.Success);
        Assert.Equal(20000m, preview.Data!.CashWithdrawn);
        Assert.Equal(30000m, preview.Data.ExpectedCash);

        var closed1 = await shiftSvc.EndShiftAsync(staffId, 30000m, "Khớp sau rút");
        Assert.True(closed1.Success);
        Assert.Equal(30000m, closed1.Data!.ExpectedCash);
        Assert.Equal(0m, closed1.Data.CashVariance);

        await Task.Delay(25);

        // Between shifts: remaining cash in drawer is 30k; 35k fails, 25k succeeds with null WorkSessionId
        Assert.False((await shiftSvc.WithdrawCashAsync(adminId, new AdminWithdrawCashRequest(35000m, "Quá số còn"))).Success);
        var w2 = await shiftSvc.WithdrawCashAsync(adminId, new AdminWithdrawCashRequest(25000m, "Nộp két tổng"));
        Assert.True(w2.Success);
        Assert.Null(w2.Data!.WorkSessionId);

        await Task.Delay(25);

        // Next shift opens with remaining 5k -> handoverDiff should be 0 (30k - 25k - 5k = 0)
        var s2 = await shiftSvc.StartShiftAsync(staffId, 5000m);
        Assert.True(s2.Success);

        var today = DateOnly.FromDateTime(DateTime.Today);
        var cashFlow = await shiftSvc.GetCashFlowAsync(today, today);
        Assert.Equal(2, cashFlow.Count);
        var row1 = cashFlow.Single(r => r.SessionId == s1.Data!.Id);
        var row2 = cashFlow.Single(r => r.SessionId == s2.Data!.Id);
        Assert.Equal(20000m, row1.CashWithdrawn);
        Assert.Equal(30000m, row1.ExpectedCash);
        Assert.Equal(0m, row2.HandoverDiff);

        var logs = await shiftSvc.GetCashTransactionsAsync(today, today);
        Assert.Equal(2, logs.Count);
        Assert.Equal("Nộp két tổng", logs[0].Reason);
        Assert.Equal("Mua đá viên", logs[1].Reason);
    }
}
