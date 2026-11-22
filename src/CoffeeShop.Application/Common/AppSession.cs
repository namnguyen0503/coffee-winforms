using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Application.Common;

public class AppSession
{
    public UserDto? CurrentUser { get; private set; }
    public WorkSessionDto? ActiveSession { get; private set; }

    public bool IsAuthenticated => CurrentUser is not null;
    public bool IsAdmin => CurrentUser?.Role == UserRole.Admin;
    public bool IsStaff => CurrentUser?.Role == UserRole.Staff;
    public bool IsWarehouseStaff => CurrentUser?.Role == UserRole.WarehouseStaff;

    public void SetUser(UserDto user)
    {
        CurrentUser = user;
    }

    public void SetActiveSession(WorkSessionDto? session)
    {
        ActiveSession = session;
    }

    public void Clear()
    {
        CurrentUser = null;
        ActiveSession = null;
    }
}
