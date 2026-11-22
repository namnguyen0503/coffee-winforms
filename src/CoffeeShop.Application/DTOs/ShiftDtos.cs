using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Application.DTOs;

public record WorkSessionDto(
    int Id,
    int UserId,
    string StaffName,
    DateTime StartTime,
    DateTime? EndTime,
    decimal StartCash,
    decimal EndCash,
    decimal CashSales,
    decimal TransferSales,
    decimal TotalSales,
    decimal ExpectedCash,
    decimal CashVariance,
    string? Note,
    WorkSessionStatus Status);

public record EndShiftSummaryDto(
    int SessionId,
    DateTime StartTime,
    decimal StartCash,
    decimal CashSales,
    decimal TransferSales,
    decimal TotalSales,
    decimal ExpectedCash,
    int OrderCount,
    decimal CashWithdrawn = 0m);

public record CashFlowRowDto(
    int SessionId,
    string StaffName,
    DateTime StartTime,
    DateTime? EndTime,
    WorkSessionStatus Status,
    decimal? PreviousEndCash,
    decimal StartCash,
    decimal HandoverDiff,
    decimal CashSales,
    decimal TransferSales,
    decimal TotalSales,
    decimal ExpectedCash,
    decimal EndCash,
    decimal CashVariance,
    string? Note,
    decimal CashWithdrawn = 0m);

public record AdminWithdrawCashRequest(decimal Amount, string Reason);

public record CashTransactionDto(
    int Id,
    int? WorkSessionId,
    int UserId,
    string StaffName,
    CashTransactionType Type,
    decimal Amount,
    string Reason,
    DateTime CreatedAt);
