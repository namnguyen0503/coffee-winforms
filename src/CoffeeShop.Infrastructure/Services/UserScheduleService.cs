using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Services;

public class UserScheduleService : IUserScheduleService
{
    private readonly IDbContextFactory<CoffeeDbContext> _dbFactory;

    public UserScheduleService(IDbContextFactory<CoffeeDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<UserDto>> GetUsersAsync(string? search = null)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var users = await db.Users.AsNoTracking()
            .OrderByDescending(u => u.Id)
            .Select(u => new UserDto(u.Id, u.FullName, u.Username, u.Role, u.IsActive, u.CreatedAt))
            .ToListAsync();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            users = users
                .Where(u =>
                    u.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                    u.Username.Contains(term, StringComparison.OrdinalIgnoreCase))
                .ToList();
        }

        return users;
    }

    public async Task<ServiceResult<UserDto>> SaveUserAsync(SaveUserRequest request)
    {
        var fullName = request.FullName?.Trim() ?? string.Empty;
        var username = request.Username?.Trim() ?? string.Empty;

        if (string.IsNullOrEmpty(fullName) || string.IsNullOrEmpty(username))
            return ServiceResult<UserDto>.Fail("Họ tên và tên đăng nhập không được để trống.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        User user;

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            var found = await db.Users.FirstOrDefaultAsync(u => u.Id == request.Id.Value);
            if (found is null)
                return ServiceResult<UserDto>.Fail("Không tìm thấy tài khoản.");
            user = found;

            var dupUsername = await db.Users.AnyAsync(u =>
                u.Username.ToLower() == username.ToLower() && u.Id != user.Id);
            if (dupUsername)
                return ServiceResult<UserDto>.Fail("Tên đăng nhập đã tồn tại.");

            user.FullName = fullName;
            user.Username = username;
            user.Role = request.Role;
            user.IsActive = request.IsActive;

            if (!string.IsNullOrWhiteSpace(request.PlainPassword))
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.PlainPassword);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(request.PlainPassword))
                return ServiceResult<UserDto>.Fail("Vui lòng nhập mật khẩu cho tài khoản mới.");

            var exists = await db.Users.AnyAsync(u => u.Username.ToLower() == username.ToLower());
            if (exists)
                return ServiceResult<UserDto>.Fail($"Tên đăng nhập '{username}' đã tồn tại!");

            user = new User
            {
                FullName = fullName,
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.PlainPassword),
                Role = request.Role,
                IsActive = request.IsActive,
                CreatedAt = DateTime.Now
            };
            db.Users.Add(user);
        }

        await db.SaveChangesAsync();

        var dto = new UserDto(user.Id, user.FullName, user.Username, user.Role, user.IsActive, user.CreatedAt);
        return ServiceResult<UserDto>.Ok(dto, request.Id.HasValue ? "Cập nhật tài khoản thành công!" : "Thêm tài khoản thành công!");
    }

    public async Task<IReadOnlyList<WorkScheduleDto>> GetSchedulesAsync(DateOnly startDate, DateOnly endDate)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.WorkSchedules.AsNoTracking()
            .Include(ws => ws.User)
            .Where(ws => ws.ShiftDate >= startDate && ws.ShiftDate <= endDate)
            .OrderBy(ws => ws.ShiftDate)
            .ThenBy(ws => ws.ShiftType)
            .ThenBy(ws => ws.User != null ? ws.User.FullName : string.Empty)
            .Select(ws => new WorkScheduleDto(
                ws.Id,
                ws.UserId,
                ws.User != null ? ws.User.FullName : "N/A",
                ws.User != null ? ws.User.Role : Domain.Enums.UserRole.Staff,
                ws.ShiftDate,
                ws.ShiftType,
                ws.CreatedAt))
            .ToListAsync();
    }

    public async Task<AddScheduleResult> AddScheduleAsync(AddScheduleRequest request)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == request.UserId);
        if (user is null)
            return new AddScheduleResult(false, "Không tìm thấy nhân viên.");

        if (!user.IsActive)
        {
            if (!request.ForceUnlock)
            {
                return new AddScheduleResult(
                    false,
                    $"Nhân viên '{user.FullName}' đang bị KHÓA. Bạn có muốn KÍCH HOẠT LẠI để phân ca không?",
                    RequiresUserUnlock: true);
            }

            user.IsActive = true;
        }

        var duplicate = await db.WorkSchedules.AnyAsync(ws =>
            ws.UserId == request.UserId &&
            ws.ShiftDate == request.ShiftDate &&
            ws.ShiftType == request.ShiftType);

        if (duplicate)
            return new AddScheduleResult(false, "Nhân viên này đã được phân vào ca này rồi!");

        db.WorkSchedules.Add(new WorkSchedule
        {
            UserId = request.UserId,
            ShiftDate = request.ShiftDate,
            ShiftType = request.ShiftType,
            CreatedAt = DateTime.Now
        });

        await db.SaveChangesAsync();
        var msg = request.ForceUnlock
            ? "Đã kích hoạt lại tài khoản và phân ca thành công!"
            : "Phân ca thành công!";
        return new AddScheduleResult(true, msg);
    }

    public async Task<ServiceResult> DeleteScheduleAsync(int scheduleId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var schedule = await db.WorkSchedules.FirstOrDefaultAsync(ws => ws.Id == scheduleId);
        if (schedule is null)
            return ServiceResult.Fail("Không tìm thấy lịch phân ca.");

        db.WorkSchedules.Remove(schedule);
        await db.SaveChangesAsync();
        return ServiceResult.Ok("Đã xóa phân ca.");
    }
}
