using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;

namespace CoffeeShop.Application.Interfaces;

public interface IUserScheduleService
{
    Task<IReadOnlyList<UserDto>> GetUsersAsync(string? search = null);
    Task<ServiceResult<UserDto>> SaveUserAsync(SaveUserRequest request);
    Task<IReadOnlyList<WorkScheduleDto>> GetSchedulesAsync(DateOnly startDate, DateOnly endDate);
    Task<AddScheduleResult> AddScheduleAsync(AddScheduleRequest request);
    Task<ServiceResult> DeleteScheduleAsync(int scheduleId);
}
