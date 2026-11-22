using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Tests;

public class VoucherAndUserScheduleTests
{
    [Fact]
    public async Task VoucherService_FullCrudAndValidationCoverage()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var svc = fx.CreateVoucherService();

        var initial = await svc.GetAllAsync();
        Assert.Equal(3, initial.Count); // WELCOME, VIP20, ADMINVIP

        // Create validation errors
        Assert.False((await svc.CreateAsync(new CreateVoucherRequest("  ", 10m, 0m, null, null))).Success);
        Assert.False((await svc.CreateAsync(new CreateVoucherRequest("BAD0", 0m, 0m, null, null))).Success);
        Assert.False((await svc.CreateAsync(new CreateVoucherRequest("BAD101", 101m, 0m, null, null))).Success);
        Assert.False((await svc.CreateAsync(new CreateVoucherRequest("BADNEG", 10m, -100m, null, null))).Success);
        Assert.False((await svc.CreateAsync(new CreateVoucherRequest("welcome", 10m, 0m, null, null))).Success); // duplicate

        // Valid create normalizes uppercase & trims
        var created = await svc.CreateAsync(new CreateVoucherRequest("  summer25 ", 25m, 60000m, "  Giảm hè ", DateTime.Now.AddDays(5)));
        Assert.True(created.Success);
        Assert.Equal("SUMMER25", created.Data!.Code);

        // Validate scenarios
        Assert.False((await svc.ValidateAsync("", 100000m, UserRole.Staff)).IsValid);
        Assert.False((await svc.ValidateAsync("MISSING", 100000m, UserRole.Admin)).IsValid);
        Assert.False((await svc.ValidateAsync("SUMMER25", 40000m, UserRole.Staff)).IsValid); // < minOrder
        Assert.True((await svc.ValidateAsync("summer25", 60000m, UserRole.Staff)).IsValid);

        // ADMINVIP role check
        var staffVip = await svc.ValidateAsync("ADMINVIP", 10000m, UserRole.Staff);
        Assert.False(staffVip.IsValid);
        var adminVip = await svc.ValidateAsync("adminvip", 10000m, UserRole.Admin);
        Assert.True(adminVip.IsValid);
        Assert.True(adminVip.RequiresAdminCustomPercent);

        // Toggle active & delete
        Assert.True((await svc.ToggleActiveAsync(created.Data.Id)).Success);
        Assert.False((await svc.ValidateAsync("SUMMER25", 100000m, UserRole.Admin)).IsValid);
        Assert.False((await svc.ToggleActiveAsync(99999)).Success);

        Assert.True((await svc.DeleteAsync(created.Data.Id)).Success);
        Assert.False((await svc.DeleteAsync(created.Data.Id)).Success);
    }

    [Fact]
    public async Task UserScheduleService_UsersAndSchedulesCrudAndValidation()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var userSvc = fx.CreateUserScheduleService();
        var authSvc = fx.CreateAuthService(new() { EnforceStaffSchedule = false });

        // Search users
        Assert.Equal(3, (await userSvc.GetUsersAsync()).Count);
        Assert.Single(await userSvc.GetUsersAsync("thukho"));

        // SaveUser validation failures
        Assert.False((await userSvc.SaveUserAsync(new SaveUserRequest(null, "", "u1", "123", UserRole.Staff, true))).Success);
        Assert.False((await userSvc.SaveUserAsync(new SaveUserRequest(null, "Test", "", "123", UserRole.Staff, true))).Success);
        Assert.False((await userSvc.SaveUserAsync(new SaveUserRequest(null, "Test", "u1", "", UserRole.Staff, true))).Success);
        Assert.False((await userSvc.SaveUserAsync(new SaveUserRequest(null, "Dup", "admin", "123", UserRole.Staff, true))).Success);
        Assert.False((await userSvc.SaveUserAsync(new SaveUserRequest(99999, "Ghost", "ghost", "123", UserRole.Staff, true))).Success);

        // Create user then edit WITHOUT password -> old password still valid
        var created = await userSvc.SaveUserAsync(new SaveUserRequest(null, "Barista A", "barista1", "pass1", UserRole.Staff, true));
        Assert.True(created.Success);
        Assert.True((await authSvc.LoginAsync("barista1", "pass1")).Success);

        var updatedKeepPass = await userSvc.SaveUserAsync(new SaveUserRequest(created.Data!.Id, "Barista A Updated", "barista1", "", UserRole.Staff, true));
        Assert.True(updatedKeepPass.Success);
        Assert.Equal("Barista A Updated", updatedKeepPass.Data!.FullName);
        Assert.True((await authSvc.LoginAsync("barista1", "pass1")).Success);

        // Edit WITH new password -> password changes
        var updatedNewPass = await userSvc.SaveUserAsync(new SaveUserRequest(created.Data.Id, "Barista A Updated", "barista1", "pass2", UserRole.Staff, true));
        Assert.True(updatedNewPass.Success);
        Assert.False((await authSvc.LoginAsync("barista1", "pass1")).Success);
        Assert.True((await authSvc.LoginAsync("barista1", "pass2")).Success);

        // Schedule CRUD
        var d = DateOnly.FromDateTime(DateTime.Today.AddDays(14));
        Assert.False((await userSvc.AddScheduleAsync(new AddScheduleRequest(99999, d, ShiftType.Morning))).Success);
        Assert.True((await userSvc.AddScheduleAsync(new AddScheduleRequest(created.Data.Id, d, ShiftType.Morning))).Success);
        Assert.False((await userSvc.AddScheduleAsync(new AddScheduleRequest(created.Data.Id, d, ShiftType.Morning))).Success); // duplicate

        var list = await userSvc.GetSchedulesAsync(d, d);
        Assert.Single(list);
        Assert.True((await userSvc.DeleteScheduleAsync(list[0].Id)).Success);
        Assert.False((await userSvc.DeleteScheduleAsync(list[0].Id)).Success);
        Assert.Empty(await userSvc.GetSchedulesAsync(d, d));
    }
}
