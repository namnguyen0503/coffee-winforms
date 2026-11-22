using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Enums;
using CoffeeShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Services;

public class VoucherService : IVoucherService
{
    private readonly IDbContextFactory<CoffeeDbContext> _dbFactory;

    public VoucherService(IDbContextFactory<CoffeeDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<IReadOnlyList<VoucherDto>> GetAllAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        return await db.Vouchers.AsNoTracking()
            .OrderByDescending(v => v.Id)
            .Select(v => new VoucherDto(
                v.Id,
                v.Code,
                v.DiscountPercent,
                v.MinOrderAmount,
                v.Description,
                v.IsActive,
                v.ExpiresAt,
                v.CreatedAt))
            .ToListAsync();
    }

    public async Task<VoucherValidationResult> ValidateAsync(string code, decimal orderSubtotal, UserRole userRole)
    {
        var normalized = code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(normalized))
            return new VoucherValidationResult(false, string.Empty, 0m, "Mã giảm giá đang trống.");

        if (normalized == "ADMINVIP")
        {
            if (userRole != UserRole.Admin)
                return new VoucherValidationResult(false, normalized, 0m, "Mã ADMINVIP chỉ dành cho Quản lý!");

            return new VoucherValidationResult(
                true,
                normalized,
                0m,
                "Nhập phần trăm giảm giá đặc quyền Admin (0-100%).",
                RequiresAdminCustomPercent: true);
        }

        await using var db = await _dbFactory.CreateDbContextAsync();
        var voucher = await db.Vouchers.AsNoTracking()
            .FirstOrDefaultAsync(v => v.Code == normalized);

        if (voucher is null)
            return new VoucherValidationResult(false, normalized, 0m, "Mã giảm giá không tồn tại!");

        if (!voucher.IsActive)
            return new VoucherValidationResult(false, normalized, 0m, "Mã giảm giá đang tạm khóa.");

        if (voucher.ExpiresAt.HasValue && voucher.ExpiresAt.Value < DateTime.Now)
            return new VoucherValidationResult(false, normalized, 0m, "Mã giảm giá đã hết hạn.");

        if (orderSubtotal < voucher.MinOrderAmount)
        {
            return new VoucherValidationResult(
                false,
                normalized,
                0m,
                $"Đơn tối thiểu {voucher.MinOrderAmount:N0} đ để dùng mã này.");
        }

        return new VoucherValidationResult(
            true,
            voucher.Code,
            voucher.DiscountPercent,
            string.IsNullOrWhiteSpace(voucher.Description)
                ? $"Giảm {voucher.DiscountPercent:N0}%"
                : voucher.Description);
    }

    public async Task<ServiceResult<VoucherDto>> CreateAsync(CreateVoucherRequest request)
    {
        var code = request.Code?.Trim().ToUpperInvariant() ?? string.Empty;
        if (string.IsNullOrEmpty(code))
            return ServiceResult<VoucherDto>.Fail("Mã giảm giá không được để trống.");
        if (request.DiscountPercent <= 0 || request.DiscountPercent > 100)
            return ServiceResult<VoucherDto>.Fail("Phần trăm giảm phải trong khoảng 1% - 100%.");
        if (request.MinOrderAmount < 0)
            return ServiceResult<VoucherDto>.Fail("Giá trị đơn tối thiểu không được âm.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        var exists = await db.Vouchers.AnyAsync(v => v.Code == code);
        if (exists)
            return ServiceResult<VoucherDto>.Fail($"Mã '{code}' đã tồn tại!");

        var entity = new Voucher
        {
            Code = code,
            DiscountPercent = request.DiscountPercent,
            MinOrderAmount = request.MinOrderAmount,
            Description = string.IsNullOrWhiteSpace(request.Description) ? null : request.Description.Trim(),
            IsActive = true,
            ExpiresAt = request.ExpiresAt,
            CreatedAt = DateTime.Now
        };

        db.Vouchers.Add(entity);
        await db.SaveChangesAsync();

        return ServiceResult<VoucherDto>.Ok(
            new VoucherDto(
                entity.Id,
                entity.Code,
                entity.DiscountPercent,
                entity.MinOrderAmount,
                entity.Description,
                entity.IsActive,
                entity.ExpiresAt,
                entity.CreatedAt),
            "Thêm mã giảm giá thành công!");
    }

    public async Task<ServiceResult> ToggleActiveAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.Id == id);
        if (voucher is null)
            return ServiceResult.Fail("Không tìm thấy mã giảm giá.");

        voucher.IsActive = !voucher.IsActive;
        await db.SaveChangesAsync();
        return ServiceResult.Ok(voucher.IsActive ? "Đã kích hoạt mã giảm giá." : "Đã tạm tắt mã giảm giá.");
    }

    public async Task<ServiceResult> DeleteAsync(int id)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var voucher = await db.Vouchers.FirstOrDefaultAsync(v => v.Id == id);
        if (voucher is null)
            return ServiceResult.Fail("Không tìm thấy mã giảm giá.");

        db.Vouchers.Remove(voucher);
        await db.SaveChangesAsync();
        return ServiceResult.Ok("Đã xóa mã giảm giá.");
    }
}
