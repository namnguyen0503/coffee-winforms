using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly IDbContextFactory<CoffeeDbContext> _dbFactory;
    private readonly AppSettings _settings;

    public AuthService(IDbContextFactory<CoffeeDbContext> dbFactory, AppSettings settings)
    {
        _dbFactory = dbFactory;
        _settings = settings;
    }

    public async Task<LoginResult> LoginAsync(string username, string password, DateTime? currentTime = null)
    {
        var normalized = username?.Trim().ToLowerInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized) || string.IsNullOrEmpty(password))
            return new LoginResult(false, "Vui lòng nhập đầy đủ tên đăng nhập và mật khẩu.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username.ToLower() == normalized);

        if (user is null)
            return new LoginResult(false, "Tài khoản không tồn tại!");

        if (!user.IsActive)
            return new LoginResult(false, "Tài khoản đang bị khóa!");

        if (!BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            return new LoginResult(false, "Mật khẩu không đúng!");

        if (user.Role == UserRole.Staff && _settings.EnforceStaffSchedule)
        {
            var now = currentTime ?? DateTime.Now;
            var currentShift = ResolveShift(now);
            if (currentShift is null)
            {
                return new LoginResult(
                    false,
                    $"Ngoài khung giờ ca làm việc ({now:HH:mm}). Giờ mở cửa từ 07:00 đến 23:30.");
            }

            var today = DateOnly.FromDateTime(now);
            var hasSchedule = await db.WorkSchedules.AsNoTracking().AnyAsync(ws =>
                ws.UserId == user.Id &&
                ws.ShiftDate == today &&
                ws.ShiftType == currentShift.Value);

            if (!hasSchedule)
            {
                return new LoginResult(
                    false,
                    $"Bạn KHÔNG CÓ LỊCH làm việc vào ca {FormatShiftName(currentShift.Value)} ({now:dd/MM/yyyy HH:mm})!");
            }
        }

        var dto = new UserDto(
            user.Id,
            user.FullName,
            user.Username,
            user.Role,
            user.IsActive,
            user.CreatedAt);

        return new LoginResult(true, "Đăng nhập thành công!", dto);
    }

    public static ShiftType? ResolveShift(DateTime time)
    {
        var t = time.TimeOfDay;
        if (t >= new TimeSpan(7, 0, 0) && t < new TimeSpan(12, 0, 0))
            return ShiftType.Morning;
        if (t >= new TimeSpan(12, 0, 0) && t < new TimeSpan(17, 0, 0))
            return ShiftType.Afternoon;
        if (t >= new TimeSpan(17, 0, 0) && t <= new TimeSpan(23, 30, 0))
            return ShiftType.Evening;
        return null;
    }

    private static string FormatShiftName(ShiftType shift) => shift switch
    {
        ShiftType.Morning => "Sáng (07:00-12:00)",
        ShiftType.Afternoon => "Chiều (12:00-17:00)",
        ShiftType.Evening => "Tối (17:00-23:30)",
        _ => shift.ToString()
    };
}
