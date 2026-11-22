using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Tests;

public class AuthServiceTests
{
    [Fact]
    public async Task Login_DefaultAccounts_SucceedsDuringShiftHours_AndHandlesCaseAndWhitespace()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var auth = fx.CreateAuthService();

        var daytime = DateTime.Today.AddHours(9);
        var adminRes = await auth.LoginAsync("  ADMIN  ", "123456", daytime);
        var staffRes = await auth.LoginAsync("StAfF", "123456", daytime);
        var whRes = await auth.LoginAsync("thukho", "123456", DateTime.Today.AddHours(2));

        Assert.True(adminRes.Success);
        Assert.Equal(UserRole.Admin, adminRes.User!.Role);
        Assert.True(staffRes.Success);
        Assert.Equal(UserRole.Staff, staffRes.User!.Role);
        Assert.True(whRes.Success);
        Assert.Equal(UserRole.WarehouseStaff, whRes.User!.Role);
    }

    [Theory]
    [InlineData("", "123456")]
    [InlineData("   ", "123456")]
    [InlineData("admin", "")]
    [InlineData("nonexistent_user", "123456")]
    [InlineData("admin", "wrongpass")]
    public async Task Login_InvalidOrMissingCredentials_Fails(string username, string password)
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var auth = fx.CreateAuthService();

        var res = await auth.LoginAsync(username, password, DateTime.Today.AddHours(10));
        Assert.False(res.Success);
        Assert.Null(res.User);
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.Staff)]
    [InlineData(UserRole.WarehouseStaff)]
    public async Task Login_LockedUser_FailsRegardlessOfRole(UserRole role)
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var userSvc = fx.CreateUserScheduleService();
        var auth = fx.CreateAuthService(new AppSettings { EnforceStaffSchedule = false });

        var created = await userSvc.SaveUserAsync(new SaveUserRequest(
            null, "Locked Account", $"locked_{role}", "123456", role, IsActive: false));
        Assert.True(created.Success);

        var res = await auth.LoginAsync($"locked_{role}", "123456", DateTime.Today.AddHours(10));
        Assert.False(res.Success);
        Assert.Contains("khóa", res.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(6, 59, 59, false)]
    [InlineData(7, 0, 0, true)]
    [InlineData(11, 59, 59, true)]
    [InlineData(12, 0, 0, true)]
    [InlineData(16, 59, 59, true)]
    [InlineData(17, 0, 0, true)]
    [InlineData(23, 30, 0, true)]
    [InlineData(23, 30, 1, false)]
    public async Task Login_StaffScheduleBoundaries_EnforcedAccurately(int hour, int minute, int second, bool expectedSuccess)
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var auth = fx.CreateAuthService();

        var testTime = DateTime.Today.AddHours(hour).AddMinutes(minute).AddSeconds(second);
        var res = await auth.LoginAsync("staff", "123456", testTime);
        Assert.Equal(expectedSuccess, res.Success);
    }

    [Fact]
    public async Task Login_StaffWithoutSpecificShiftSchedule_IsBlockedEvenIfAnotherShiftSameDayExists()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var auth = fx.CreateAuthService();

        // Remove ONLY morning schedule for today; afternoon & evening remain
        await using (var db = await fx.DbFactory.CreateDbContextAsync())
        {
            var today = DateOnly.FromDateTime(DateTime.Today);
            var morning = await db.WorkSchedules
                .Where(ws => ws.ShiftDate == today && ws.ShiftType == ShiftType.Morning)
                .ToListAsync();
            db.WorkSchedules.RemoveRange(morning);
            await db.SaveChangesAsync();
        }

        var morningRes = await auth.LoginAsync("staff", "123456", DateTime.Today.AddHours(9));
        Assert.False(morningRes.Success);

        var afternoonRes = await auth.LoginAsync("staff", "123456", DateTime.Today.AddHours(14));
        Assert.True(afternoonRes.Success);
    }

    [Fact]
    public async Task Login_EnforceStaffScheduleFalse_AllowsStaffAtAnyHourWithoutSchedule()
    {
        await using var fx = new TestDbFixture();
        await fx.InitializeAndSeedAsync();
        var userSvc = fx.CreateUserScheduleService();
        var auth = fx.CreateAuthService(new AppSettings { EnforceStaffSchedule = false });

        var newStaff = await userSvc.SaveUserAsync(new SaveUserRequest(
            null, "Unscheduled Staff", "staff2", "123456", UserRole.Staff, true));
        Assert.True(newStaff.Success);

        var res = await auth.LoginAsync("staff2", "123456", DateTime.Today.AddHours(3));
        Assert.True(res.Success);
    }
}
