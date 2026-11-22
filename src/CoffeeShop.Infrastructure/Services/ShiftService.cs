using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Entities;
using CoffeeShop.Domain.Enums;
using CoffeeShop.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace CoffeeShop.Infrastructure.Services;

public class ShiftService : IShiftService
{
    private readonly IDbContextFactory<CoffeeDbContext> _dbFactory;

    public ShiftService(IDbContextFactory<CoffeeDbContext> dbFactory)
    {
        _dbFactory = dbFactory;
    }

    public async Task<WorkSessionDto?> GetActiveSessionAsync(int userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var session = await db.WorkSessions.AsNoTracking()
            .Include(ws => ws.User)
            .FirstOrDefaultAsync(ws => ws.UserId == userId && ws.Status == WorkSessionStatus.Open);

        return session is null ? null : MapDto(session);
    }

    public async Task<WorkSessionDto?> GetAnyActiveSessionAsync()
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var session = await db.WorkSessions.AsNoTracking()
            .Include(ws => ws.User)
            .OrderByDescending(ws => ws.Id)
            .FirstOrDefaultAsync(ws => ws.Status == WorkSessionStatus.Open);

        return session is null ? null : MapDto(session);
    }

    public async Task<ServiceResult<WorkSessionDto>> StartShiftAsync(int userId, decimal startCash)
    {
        if (startCash < 0)
            return ServiceResult<WorkSessionDto>.Fail("Tiền đầu ca không được âm.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == userId && u.IsActive);
        if (user is null)
            return ServiceResult<WorkSessionDto>.Fail("Tài khoản không hợp lệ hoặc đã bị khóa.");

        var hasSelfOpen = await db.WorkSessions.AnyAsync(ws =>
            ws.UserId == userId && ws.Status == WorkSessionStatus.Open);
        if (hasSelfOpen)
            return ServiceResult<WorkSessionDto>.Fail("Bạn đang trong một ca làm việc rồi.");

        var now = DateTime.Now;
        var otherOpenSessions = await db.WorkSessions
            .Where(ws => ws.Status == WorkSessionStatus.Open)
            .ToListAsync();

        var forcedOldClose = false;
        foreach (var oldSession in otherOpenSessions)
        {
            var sales = await ComputeSessionSalesAsync(db, oldSession.Id);
            var withdrawn = await ComputeSessionWithdrawnAsync(db, oldSession.Id);
            oldSession.EndTime = now;
            oldSession.CashSales = sales.CashSales;
            oldSession.TransferSales = sales.TransferSales;
            oldSession.TotalSales = sales.CashSales + sales.TransferSales;
            oldSession.EndCash = startCash;
            oldSession.ExpectedCash = oldSession.StartCash + sales.CashSales - withdrawn;
            oldSession.CashVariance = startCash - oldSession.ExpectedCash;
            oldSession.Note = "Hệ thống tự chốt do nhân viên mới vào ca.";
            oldSession.Status = WorkSessionStatus.Closed;
            forcedOldClose = true;
        }

        var newSession = new WorkSession
        {
            UserId = userId,
            StartTime = now,
            StartCash = startCash,
            EndCash = 0m,
            CashSales = 0m,
            TransferSales = 0m,
            TotalSales = 0m,
            ExpectedCash = startCash,
            CashVariance = 0m,
            Status = WorkSessionStatus.Open
        };

        db.WorkSessions.Add(newSession);
        await db.SaveChangesAsync();
        await tx.CommitAsync();

        newSession.User = user;
        var msg = forcedOldClose
            ? "Đã tự động chốt ca trước và mở ca mới thành công!"
            : "Bắt đầu ca làm việc thành công!";

        return ServiceResult<WorkSessionDto>.Ok(MapDto(newSession), msg);
    }

    public async Task<ServiceResult<EndShiftSummaryDto>> GetCurrentShiftPreviewAsync(int userId)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();
        var session = await db.WorkSessions.AsNoTracking()
            .FirstOrDefaultAsync(ws => ws.UserId == userId && ws.Status == WorkSessionStatus.Open);

        if (session is null)
            return ServiceResult<EndShiftSummaryDto>.Fail("Bạn chưa mở ca làm việc nào.");

        var sales = await ComputeSessionSalesAsync(db, session.Id);
        var withdrawn = await ComputeSessionWithdrawnAsync(db, session.Id);
        var summary = new EndShiftSummaryDto(
            session.Id,
            session.StartTime,
            session.StartCash,
            sales.CashSales,
            sales.TransferSales,
            sales.CashSales + sales.TransferSales,
            session.StartCash + sales.CashSales - withdrawn,
            sales.OrderCount,
            withdrawn);

        return ServiceResult<EndShiftSummaryDto>.Ok(summary);
    }

    public async Task<ServiceResult<WorkSessionDto>> EndShiftAsync(int userId, decimal endCash, string? note)
    {
        if (endCash < 0)
            return ServiceResult<WorkSessionDto>.Fail("Tiền mặt thực tế không được âm.");

        await using var db = await _dbFactory.CreateDbContextAsync();
        await using var tx = await db.Database.BeginTransactionAsync();

        var session = await db.WorkSessions
            .Include(ws => ws.User)
            .FirstOrDefaultAsync(ws => ws.UserId == userId && ws.Status == WorkSessionStatus.Open);

        if (session is null)
            return ServiceResult<WorkSessionDto>.Fail("Không tìm thấy ca làm việc đang mở.");

        var sales = await ComputeSessionSalesAsync(db, session.Id);
        var withdrawn = await ComputeSessionWithdrawnAsync(db, session.Id);

        session.EndTime = DateTime.Now;
        session.CashSales = sales.CashSales;
        session.TransferSales = sales.TransferSales;
        session.TotalSales = sales.CashSales + sales.TransferSales;
        session.EndCash = endCash;
        session.ExpectedCash = session.StartCash + sales.CashSales - withdrawn;
        session.CashVariance = endCash - session.ExpectedCash;
        session.Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
        session.Status = WorkSessionStatus.Closed;

        await db.SaveChangesAsync();
        await tx.CommitAsync();

        return ServiceResult<WorkSessionDto>.Ok(
            MapDto(session),
            $"Chốt ca thành công! Doanh thu: {session.TotalSales:N0} đ");
    }

    public async Task<IReadOnlyList<CashFlowRowDto>> GetCashFlowAsync(DateOnly startDate, DateOnly endDate, bool diffOnly = false)
    {
        var startDt = startDate.ToDateTime(TimeOnly.MinValue);
        var endDt = endDate.ToDateTime(TimeOnly.MaxValue);

        await using var db = await _dbFactory.CreateDbContextAsync();
        var allSessions = await db.WorkSessions.AsNoTracking()
            .Include(ws => ws.User)
            .Where(ws => ws.StartTime <= endDt)
            .OrderBy(ws => ws.Id)
            .ToListAsync();

        var allWithdrawals = await db.CashTransactions.AsNoTracking()
            .Where(ct => ct.Type == CashTransactionType.AdminWithdrawal && ct.CreatedAt <= endDt)
            .Select(ct => new { ct.WorkSessionId, ct.Amount, ct.CreatedAt })
            .ToListAsync();

        var rows = new List<CashFlowRowDto>();
        decimal? previousEndCash = null;
        DateTime? previousClosedEndTime = null;

        foreach (var s in allSessions)
        {
            var inShiftWithdrawn = allWithdrawals
                .Where(w => w.WorkSessionId == s.Id)
                .Sum(w => w.Amount);

            var betweenWithdrawn = (previousEndCash.HasValue && previousClosedEndTime.HasValue)
                ? allWithdrawals
                    .Where(w => !w.WorkSessionId.HasValue
                        && w.CreatedAt >= previousClosedEndTime.Value
                        && w.CreatedAt <= s.StartTime)
                    .Sum(w => w.Amount)
                : 0m;

            var expectedHandoverBaseline = previousEndCash.HasValue
                ? previousEndCash.Value - betweenWithdrawn
                : (decimal?)null;

            var handoverDiff = expectedHandoverBaseline.HasValue
                ? s.StartCash - expectedHandoverBaseline.Value
                : 0m;

            var hasVariance = handoverDiff != 0m || (s.Status == WorkSessionStatus.Closed && s.CashVariance != 0m);

            if (s.StartTime >= startDt && s.StartTime <= endDt && (!diffOnly || hasVariance))
            {
                decimal cashSales = s.CashSales;
                decimal transferSales = s.TransferSales;
                decimal totalSales = s.TotalSales;
                decimal expectedCash = s.ExpectedCash;

                if (s.Status == WorkSessionStatus.Open)
                {
                    var liveSales = await ComputeSessionSalesAsync(db, s.Id);
                    cashSales = liveSales.CashSales;
                    transferSales = liveSales.TransferSales;
                    totalSales = cashSales + transferSales;
                    expectedCash = s.StartCash + cashSales - inShiftWithdrawn;
                }

                rows.Add(new CashFlowRowDto(
                    s.Id,
                    s.User?.FullName ?? "N/A",
                    s.StartTime,
                    s.EndTime,
                    s.Status,
                    previousEndCash,
                    s.StartCash,
                    handoverDiff,
                    cashSales,
                    transferSales,
                    totalSales,
                    expectedCash,
                    s.EndCash,
                    s.CashVariance,
                    s.Note,
                    inShiftWithdrawn));
            }

            if (s.Status == WorkSessionStatus.Closed)
            {
                previousEndCash = s.EndCash;
                previousClosedEndTime = s.EndTime ?? s.StartTime;
            }
        }

        rows.Reverse();
        return rows;
    }

    public async Task<ServiceResult<CashTransactionDto>> WithdrawCashAsync(int adminUserId, AdminWithdrawCashRequest request)
    {
        if (request.Amount <= 0)
            return ServiceResult<CashTransactionDto>.Fail("Số tiền rút phải lớn hơn 0.");

        if (string.IsNullOrWhiteSpace(request.Reason))
            return ServiceResult<CashTransactionDto>.Fail("Vui lòng nhập lý do rút tiền.");

        await using var db = await _dbFactory.CreateDbContextAsync();

        var admin = await db.Users.FirstOrDefaultAsync(u => u.Id == adminUserId && u.IsActive);
        if (admin is null || admin.Role != UserRole.Admin)
            return ServiceResult<CashTransactionDto>.Fail("Chỉ Admin mới có quyền rút tiền từ két.");

        var openSession = await db.WorkSessions
            .Where(ws => ws.Status == WorkSessionStatus.Open)
            .OrderByDescending(ws => ws.Id)
            .FirstOrDefaultAsync();

        int? workSessionId;
        decimal availableCash;

        if (openSession is not null)
        {
            var sales = await ComputeSessionSalesAsync(db, openSession.Id);
            var withdrawn = await ComputeSessionWithdrawnAsync(db, openSession.Id);
            availableCash = openSession.StartCash + sales.CashSales - withdrawn;
            workSessionId = openSession.Id;
        }
        else
        {
            var lastClosed = await db.WorkSessions.AsNoTracking()
                .Where(ws => ws.Status == WorkSessionStatus.Closed)
                .OrderByDescending(ws => ws.Id)
                .FirstOrDefaultAsync();

            if (lastClosed is null)
                return ServiceResult<CashTransactionDto>.Fail("Két hiện tại chưa có tiền mặt để rút (chưa có ca mở hoặc ca đã chốt).");

            var gapStart = lastClosed.EndTime ?? lastClosed.StartTime;
            var betweenWithdrawn = (await db.CashTransactions.AsNoTracking()
                .Where(ct => ct.WorkSessionId == null
                    && ct.Type == CashTransactionType.AdminWithdrawal
                    && ct.CreatedAt >= gapStart)
                .Select(ct => ct.Amount)
                .ToListAsync())
                .Sum();

            availableCash = lastClosed.EndCash - betweenWithdrawn;
            workSessionId = null;
        }

        if (request.Amount > availableCash)
        {
            return ServiceResult<CashTransactionDto>.Fail(
                $"Số tiền rút ({request.Amount:N0} đ) vượt quá tiền mặt hiện có trong két ({availableCash:N0} đ).");
        }

        var entity = new CashTransaction
        {
            WorkSessionId = workSessionId,
            UserId = adminUserId,
            Type = CashTransactionType.AdminWithdrawal,
            Amount = request.Amount,
            Reason = request.Reason.Trim(),
            CreatedAt = DateTime.Now
        };

        db.CashTransactions.Add(entity);
        await db.SaveChangesAsync();

        return ServiceResult<CashTransactionDto>.Ok(
            new CashTransactionDto(
                entity.Id,
                entity.WorkSessionId,
                entity.UserId,
                admin.FullName,
                entity.Type,
                entity.Amount,
                entity.Reason,
                entity.CreatedAt),
            "Đã ghi nhận rút tiền khỏi két.");
    }

    public async Task<IReadOnlyList<CashTransactionDto>> GetCashTransactionsAsync(DateOnly startDate, DateOnly endDate)
    {
        await using var db = await _dbFactory.CreateDbContextAsync();

        var startDt = startDate.ToDateTime(TimeOnly.MinValue);
        var endDt = endDate.ToDateTime(TimeOnly.MaxValue);

        var list = await db.CashTransactions.AsNoTracking()
            .Include(ct => ct.User)
            .Where(ct => ct.CreatedAt >= startDt && ct.CreatedAt <= endDt)
            .OrderByDescending(ct => ct.CreatedAt)
            .ThenByDescending(ct => ct.Id)
            .ToListAsync();

        return list.Select(ct => new CashTransactionDto(
            ct.Id,
            ct.WorkSessionId,
            ct.UserId,
            ct.User?.FullName ?? "N/A",
            ct.Type,
            ct.Amount,
            ct.Reason,
            ct.CreatedAt)).ToList();
    }

    private static async Task<decimal> ComputeSessionWithdrawnAsync(CoffeeDbContext db, int sessionId)
    {
        var amounts = await db.CashTransactions.AsNoTracking()
            .Where(ct => ct.WorkSessionId == sessionId && ct.Type == CashTransactionType.AdminWithdrawal)
            .Select(ct => ct.Amount)
            .ToListAsync();

        return amounts.Sum();
    }

    private static async Task<(decimal CashSales, decimal TransferSales, int OrderCount)> ComputeSessionSalesAsync(
        CoffeeDbContext db,
        int sessionId)
    {
        var paidOrders = await db.Orders.AsNoTracking()
            .Where(o => o.WorkSessionId == sessionId && o.Status == OrderStatus.Paid)
            .Select(o => new { o.PaymentMethod, o.FinalAmount })
            .ToListAsync();

        decimal cash = 0m;
        decimal transfer = 0m;
        foreach (var order in paidOrders)
        {
            if (order.PaymentMethod == PaymentMethod.Cash)
                cash += order.FinalAmount;
            else
                transfer += order.FinalAmount;
        }

        return (cash, transfer, paidOrders.Count);
    }

    private static WorkSessionDto MapDto(WorkSession ws) => new(
        ws.Id,
        ws.UserId,
        ws.User?.FullName ?? "N/A",
        ws.StartTime,
        ws.EndTime,
        ws.StartCash,
        ws.EndCash,
        ws.CashSales,
        ws.TransferSales,
        ws.TotalSales,
        ws.ExpectedCash,
        ws.CashVariance,
        ws.Note,
        ws.Status);
}
