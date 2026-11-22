using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Application.DTOs;

public record SaveUserRequest(
    int? Id,
    string FullName,
    string Username,
    string? PlainPassword,
    UserRole Role,
    bool IsActive);

public record WorkScheduleDto(
    int Id,
    int UserId,
    string FullName,
    UserRole Role,
    DateOnly ShiftDate,
    ShiftType ShiftType,
    DateTime CreatedAt);

public record AddScheduleRequest(
    int UserId,
    DateOnly ShiftDate,
    ShiftType ShiftType,
    bool ForceUnlock = false);

public record AddScheduleResult(
    bool Success,
    string Message,
    bool RequiresUserUnlock = false);
