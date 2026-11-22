using CoffeeShop.Application.Common;
using CoffeeShop.Infrastructure;
using CoffeeShop.Infrastructure.Data;
using CoffeeShop.Infrastructure.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.Tests;

public sealed class TestDbFixture : IAsyncDisposable
{
    private readonly SqliteConnection _connection;
    public IDbContextFactory<CoffeeDbContext> DbFactory { get; }
    public AppSettings Settings { get; } = new() { EnforceStaffSchedule = true };

    public TestDbFixture()
    {
        _connection = new SqliteConnection("DataSource=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<CoffeeDbContext>()
            .UseSqlite(_connection)
            .Options;

        DbFactory = new PooledSqliteFactory(options);
    }

    public async Task InitializeEmptyAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
    }

    public async Task InitializeAndSeedAsync()
    {
        await using var db = await DbFactory.CreateDbContextAsync();
        await db.Database.EnsureCreatedAsync();
        await DbSeeder.SeedAsync(db);
    }

    public AuthService CreateAuthService(AppSettings? settings = null) => new(DbFactory, settings ?? Settings);
    public ShiftService CreateShiftService() => new(DbFactory);
    public MenuService CreateMenuService() => new(DbFactory);
    public OrderService CreateOrderService() => new(DbFactory);
    public InventoryService CreateInventoryService() => new(DbFactory);
    public VoucherService CreateVoucherService() => new(DbFactory);
    public UserScheduleService CreateUserScheduleService() => new(DbFactory);
    public ReportService CreateReportService() => new(DbFactory);
    public CsvExportService CreateCsvExportService() => new();

    public ServiceProvider CreateServiceProvider(AppSettings? settings = null)
    {
        var services = new ServiceCollection();
        services.AddSingleton(DbFactory);
        services.AddSingleton(settings ?? Settings);
        services.AddSingleton<AppSession>();
        services.AddCoffeeShopServices();
        return services.BuildServiceProvider();
    }

    public async ValueTask DisposeAsync()
    {
        await _connection.DisposeAsync();
    }

    private sealed class PooledSqliteFactory : IDbContextFactory<CoffeeDbContext>
    {
        private readonly DbContextOptions<CoffeeDbContext> _options;
        public PooledSqliteFactory(DbContextOptions<CoffeeDbContext> options) => _options = options;
        public CoffeeDbContext CreateDbContext() => new(_options);
    }
}
