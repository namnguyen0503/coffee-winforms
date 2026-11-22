using CoffeeShop.Application.Common;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Infrastructure;
using CoffeeShop.Infrastructure.Data;
using CoffeeShop.WinForms.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinForms;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();
        System.Windows.Forms.Application.SetHighDpiMode(HighDpiMode.PerMonitorV2);
        System.Windows.Forms.Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        System.Windows.Forms.Application.ThreadException += (_, e) =>
            MessageBox.Show(
                $"Đã xảy ra lỗi giao diện: {e.Exception.Message}",
                "Lỗi hệ thống",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);

        var configuration = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .Build();

        var connectionString = configuration.GetConnectionString("DefaultConnection")
            ?? throw new InvalidOperationException("Thiếu ConnectionStrings:DefaultConnection trong appsettings.json.");

        var appSection = configuration.GetSection("AppSettings");
        var appSettings = new AppSettings
        {
            EnforceStaffSchedule = !bool.TryParse(appSection["EnforceStaffSchedule"], out var enforce) || enforce,
            StoreName = appSection["StoreName"] ?? "Nguyễn Văn Coffee",
            StoreAddress = appSection["StoreAddress"] ?? "Hải Phòng, Việt Nam",
            WifiInfo = appSection["WifiInfo"] ?? "Wifi: nguyenvan_coffee / Pass: 12345678",
            BankCode = appSection["BankCode"] ?? "MB",
            BankAccountNo = appSection["BankAccountNo"] ?? "0123456789",
            BankAccountName = appSection["BankAccountName"] ?? "NGUYEN VAN COFFEE"
        };

        var services = new ServiceCollection();
        services.AddSingleton(appSettings);
        services.AddSingleton<AppSession>();
        services.AddCoffeeShopInfrastructure(connectionString);

        using var serviceProvider = services.BuildServiceProvider();

        try
        {
            InitializeDatabaseAsync(serviceProvider).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                "Không thể kết nối hoặc khởi tạo SQL Server Database!\n\n" +
                $"Chi tiết: {ex.Message}\n\n" +
                "Vui lòng kiểm tra:\n" +
                "1. Dịch vụ SQL Server (MSSQLSERVER) đã được khởi động chưa.\n" +
                "2. Chuỗi kết nối ConnectionStrings:DefaultConnection trong file appsettings.json.",
                "Lỗi kết nối SQL Server",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
            return;
        }

        while (true)
        {
            var appSession = serviceProvider.GetRequiredService<AppSession>();
            appSession.Clear();

            using (var loginForm = new LoginForm(
                serviceProvider.GetRequiredService<IAuthService>(),
                appSession,
                appSettings))
            {
                if (loginForm.ShowDialog() != DialogResult.OK || !appSession.IsAuthenticated)
                    break;
            }

            using var mainForm = new MainForm(
                serviceProvider,
                appSession,
                appSettings,
                serviceProvider.GetRequiredService<IShiftService>());

            System.Windows.Forms.Application.Run(mainForm);

            if (!mainForm.LoggedOut)
                break;
        }
    }

    private static async Task InitializeDatabaseAsync(IServiceProvider serviceProvider)
    {
        var dbFactory = serviceProvider.GetRequiredService<IDbContextFactory<CoffeeDbContext>>();
        await using var db = await dbFactory.CreateDbContextAsync();
        await DbSchemaUpgrader.EnsureLatestSchemaAsync(db);
        await DbSeeder.SeedAsync(db);
    }
}
