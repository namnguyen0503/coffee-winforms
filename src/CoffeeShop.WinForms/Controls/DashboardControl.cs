using CoffeeShop.Application.Common;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Controls;

public class DashboardControl : UserControl
{
    private readonly IReportService _reportService;
    private readonly IShiftService _shiftService;
    private readonly AppSession _appSession;
    private readonly Action<string> _navigateTo;

    private readonly Label _lblRevenueVal;
    private readonly Label _lblOrdersVal;
    private readonly Label _lblLowStockVal;
    private readonly Label _lblShiftVal;
    private readonly DataGridView _gridLowStock = new();
    private readonly DataGridView _gridRecentOrders = new();

    public DashboardControl(
        IReportService reportService,
        IShiftService shiftService,
        AppSession appSession,
        Action<string> navigateTo)
    {
        _reportService = reportService;
        _shiftService = shiftService;
        _appSession = appSession;
        _navigateTo = navigateTo;

        Dock = DockStyle.Fill;
        BackColor = CoffeeTheme.Cream;
        Padding = new Padding(16);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 110));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        // Top KPI Row
        var kpiPanel = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 4,
            RowCount = 1
        };
        for (int i = 0; i < 4; i++)
            kpiPanel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        kpiPanel.Controls.Add(CoffeeTheme.CreateKpiCard("Doanh thu hôm nay", "0 đ", CoffeeTheme.AccentGreen, out _lblRevenueVal), 0, 0);
        kpiPanel.Controls.Add(CoffeeTheme.CreateKpiCard("Đơn đã thanh toán", "0", CoffeeTheme.Mocha, out _lblOrdersVal), 1, 0);
        kpiPanel.Controls.Add(CoffeeTheme.CreateKpiCard("Nguyên liệu sắp hết", "0", CoffeeTheme.WarningOrange, out _lblLowStockVal), 2, 0);
        kpiPanel.Controls.Add(CoffeeTheme.CreateKpiCard("Trạng thái ca", "Chưa vào ca", CoffeeTheme.AccentBlue, out _lblShiftVal), 3, 0);

        // Quick actions bar
        var actionBar = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(4, 8, 4, 4)
        };
        var btnOpenPos = CoffeeTheme.CreatePrimaryButton("Mở màn hình POS Bán hàng", 210, 40);
        btnOpenPos.Click += (_, _) => _navigateTo("pos");
        actionBar.Controls.Add(btnOpenPos);

        if (_appSession.IsAdmin)
        {
            var btnReports = CoffeeTheme.CreateSecondaryButton("Xem báo cáo lợi nhuận", 180, 40);
            btnReports.Click += (_, _) => _navigateTo("reports");
            actionBar.Controls.Add(btnReports);

            var btnWarehouse = CoffeeTheme.CreateSecondaryButton("Kiểm tra kho nguyên liệu", 190, 40);
            btnWarehouse.Click += (_, _) => _navigateTo("warehouse");
            actionBar.Controls.Add(btnWarehouse);

            var btnWithdrawCash = CoffeeTheme.CreateDangerButton("Rút tiền két (Admin)", 175, 40);
            btnWithdrawCash.Click += (_, _) => _navigateTo("reports");
            actionBar.Controls.Add(btnWithdrawCash);
        }

        var btnRefresh = CoffeeTheme.CreateSecondaryButton("Làm mới", 110, 40);
        btnRefresh.Click += async (_, _) => await LoadAsync();
        actionBar.Controls.Add(btnRefresh);

        // Bottom split: Recent Orders & Low Stock Alerts
        var bottomSplit = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1
        };
        bottomSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60));
        bottomSplit.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40));

        var recentCard = CoffeeTheme.CreateCardPanel();
        recentCard.Dock = DockStyle.Fill;
        recentCard.Margin = new Padding(6);
        var lblRecent = new Label { Text = "Đơn hàng gần đây", Font = CoffeeTheme.SectionFont, Dock = DockStyle.Top, Height = 30 };
        _gridRecentOrders.Dock = DockStyle.Fill;
        _gridRecentOrders.ReadOnly = true;
        CoffeeTheme.StyleDataGridView(_gridRecentOrders);
        _gridRecentOrders.Columns.Add("OrderCode", "Mã đơn");
        _gridRecentOrders.Columns.Add("Time", "Giờ");
        _gridRecentOrders.Columns.Add("Staff", "Nhân viên");
        _gridRecentOrders.Columns.Add("Amount", "Thực thu");
        _gridRecentOrders.Columns.Add("Status", "Trạng thái");
        recentCard.Controls.Add(_gridRecentOrders);
        recentCard.Controls.Add(lblRecent);

        var stockCard = CoffeeTheme.CreateCardPanel();
        stockCard.Dock = DockStyle.Fill;
        stockCard.Margin = new Padding(6);
        var lblStock = new Label { Text = "Cảnh báo kho sắp hết", Font = CoffeeTheme.SectionFont, ForeColor = CoffeeTheme.WarningOrange, Dock = DockStyle.Top, Height = 30 };
        _gridLowStock.Dock = DockStyle.Fill;
        _gridLowStock.ReadOnly = true;
        CoffeeTheme.StyleDataGridView(_gridLowStock);
        _gridLowStock.Columns.Add("Name", "Nguyên liệu");
        _gridLowStock.Columns.Add("Qty", "Tồn hiện tại");
        _gridLowStock.Columns.Add("Min", "Ngưỡng");
        stockCard.Controls.Add(_gridLowStock);
        stockCard.Controls.Add(lblStock);

        bottomSplit.Controls.Add(recentCard, 0, 0);
        bottomSplit.Controls.Add(stockCard, 1, 0);

        root.Controls.Add(kpiPanel, 0, 0);
        root.Controls.Add(actionBar, 0, 1);
        root.Controls.Add(bottomSplit, 0, 2);
        Controls.Add(root);
    }

    public async Task LoadAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is null) return;

        int? filterStaffId = user.Role == UserRole.Staff ? user.Id : null;
        var summary = await _reportService.GetDashboardSummaryAsync(filterStaffId);
        var activeShift = await _shiftService.GetActiveSessionAsync(user.Id);
        _appSession.SetActiveSession(activeShift);

        _lblRevenueVal.Text = FormatHelper.Currency(summary.TodayRevenue);
        _lblOrdersVal.Text = summary.TodayOrderCount.ToString();
        _lblLowStockVal.Text = summary.LowStockCount.ToString();
        _lblShiftVal.Text = activeShift is null ? "Chưa mở ca" : $"Ca #{activeShift.Id} ({activeShift.StartTime:HH:mm})";

        _gridRecentOrders.Rows.Clear();
        foreach (var o in summary.RecentOrders)
        {
            var idx = _gridRecentOrders.Rows.Add(
                o.OrderCode,
                o.OrderDate.ToString("HH:mm dd/MM"),
                o.StaffName,
                FormatHelper.Currency(o.FinalAmount),
                FormatHelper.OrderStatusLabel(o.Status));
            if (o.Status == OrderStatus.Cancelled)
                _gridRecentOrders.Rows[idx].DefaultCellStyle.ForeColor = CoffeeTheme.DangerRed;
        }

        _gridLowStock.Rows.Clear();
        foreach (var i in summary.LowStockIngredients)
        {
            _gridLowStock.Rows.Add(
                i.Name,
                $"{i.Quantity:0.##} {i.Unit}",
                $"{i.MinQuantity:0.##} {i.Unit}");
        }
    }
}
