using CoffeeShop.Application.Interfaces;
using CoffeeShop.Infrastructure.Data;
using CoffeeShop.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCoffeeShopInfrastructure(
        this IServiceCollection services,
        string connectionString)
    {
        services.AddDbContextFactory<CoffeeDbContext>(options =>
            options.UseSqlServer(connectionString));

        return services.AddCoffeeShopServices();
    }

    public static IServiceCollection AddCoffeeShopServices(this IServiceCollection services)
    {
        services.AddSingleton<IAuthService, AuthService>();
        services.AddSingleton<IShiftService, ShiftService>();
        services.AddSingleton<IMenuService, MenuService>();
        services.AddSingleton<IOrderService, OrderService>();
        services.AddSingleton<IInventoryService, InventoryService>();
        services.AddSingleton<IVoucherService, VoucherService>();
        services.AddSingleton<IUserScheduleService, UserScheduleService>();
        services.AddSingleton<IReportService, ReportService>();
        services.AddSingleton<ICsvExportService, CsvExportService>();
        return services;
    }
}
