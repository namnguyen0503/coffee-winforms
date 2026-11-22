using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Forms;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Controls;

public class OrdersControl : UserControl
{
    private readonly IOrderService _orderService;
    private readonly ICsvExportService _csvExportService;
    private readonly AppSession _appSession;
    private readonly AppSettings _settings;

    private readonly DateTimePicker _dtStart = new();
    private readonly DateTimePicker _dtEnd = new();
    private readonly TextBox _txtSearch = new();
    private readonly DataGridView _grid = new();
    private readonly Button _btnCancelOrder;

    private List<OrderDto> _currentOrders = new();

    public OrdersControl(
        IOrderService orderService,
        ICsvExportService csvExportService,
        AppSession appSession,
        AppSettings? settings = null)
    {
        _orderService = orderService;
        _csvExportService = csvExportService;
        _appSession = appSession;
        _settings = settings ?? new AppSettings();

        Dock = DockStyle.Fill;
        BackColor = CoffeeTheme.Cream;
        Padding = new Padding(16);

        var filterPanel = CoffeeTheme.CreateCardPanel(new Padding(12));
        filterPanel.Dock = DockStyle.Top;
        filterPanel.Height = 62;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _dtStart.Format = DateTimePickerFormat.Short;
        _dtStart.Width = 115;
        _dtStart.Value = DateTime.Today.AddDays(-7);

        _dtEnd.Format = DateTimePickerFormat.Short;
        _dtEnd.Width = 115;
        _dtEnd.Value = DateTime.Today;

        _txtSearch.Width = 220;
        _txtSearch.PlaceholderText = "Mã đơn hoặc tên nhân viên...";

        var btnFilter = CoffeeTheme.CreatePrimaryButton("Tìm kiếm", 100, 32);
        btnFilter.Click += async (_, _) => await LoadAsync();

        var btnDetail = CoffeeTheme.CreateSecondaryButton("Xem chi tiết", 110, 32);
        btnDetail.Click += (_, _) => ShowSelectedOrderDetail();

        _btnCancelOrder = CoffeeTheme.CreateDangerButton("Hủy đơn (Admin)", 130, 32);
        _btnCancelOrder.Visible = _appSession.IsAdmin;
        _btnCancelOrder.Click += async (_, _) => await CancelSelectedOrderAsync();

        var btnExport = CoffeeTheme.CreateSecondaryButton("Xuất CSV", 100, 32);
        btnExport.Click += (_, _) => ExportCsv();

        flow.Controls.AddRange(new Control[]
        {
            new Label { Text = "Từ:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) },
            _dtStart,
            new Label { Text = "Đến:", AutoSize = true, Padding = new Padding(8, 8, 4, 0) },
            _dtEnd,
            _txtSearch,
            btnFilter,
            btnDetail,
            _btnCancelOrder,
            btnExport
        });
        filterPanel.Controls.Add(flow);

        var gridCard = CoffeeTheme.CreateCardPanel();
        gridCard.Dock = DockStyle.Fill;
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;
        CoffeeTheme.StyleDataGridView(_grid);
        _grid.CellDoubleClick += (_, _) => ShowSelectedOrderDetail();

        _grid.Columns.Add("OrderCode", "Mã đơn");
        _grid.Columns.Add("OrderDate", "Thời gian");
        _grid.Columns.Add("StaffName", "Thu ngân");
        _grid.Columns.Add("Payment", "Thanh toán");
        _grid.Columns.Add("Voucher", "Voucher");
        _grid.Columns.Add("FinalAmount", "Thực thu");
        _grid.Columns.Add("Status", "Trạng thái");
        _grid.Columns.Add("CancelReason", "Ghi chú / Hủy");

        gridCard.Controls.Add(_grid);
        Controls.Add(gridCard);
        Controls.Add(filterPanel);
    }

    public async Task LoadAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is null) return;

        _btnCancelOrder.Visible = user.Role == UserRole.Admin;

        var filter = new OrderFilterRequest(
            DateOnly.FromDateTime(_dtStart.Value),
            DateOnly.FromDateTime(_dtEnd.Value),
            _txtSearch.Text,
            user.Role == UserRole.Staff ? user.Id : null);

        _currentOrders = (await _orderService.GetOrdersAsync(filter)).ToList();

        _grid.Rows.Clear();
        foreach (var o in _currentOrders)
        {
            var idx = _grid.Rows.Add(
                o.OrderCode,
                o.OrderDate.ToString("dd/MM/yyyy HH:mm"),
                o.StaffName,
                FormatHelper.PaymentLabel(o.PaymentMethod),
                string.IsNullOrEmpty(o.VoucherCode) ? "-" : $"{o.VoucherCode} (-{o.DiscountPercent:0.##}%)",
                FormatHelper.Currency(o.FinalAmount),
                FormatHelper.OrderStatusLabel(o.Status),
                o.CancelReason ?? string.Empty);

            if (o.Status == OrderStatus.Cancelled)
                _grid.Rows[idx].DefaultCellStyle.ForeColor = CoffeeTheme.DangerRed;
        }
    }

    private OrderDto? GetSelectedOrder()
    {
        if (_grid.CurrentRow is null) return null;
        var idx = _grid.CurrentRow.Index;
        return idx >= 0 && idx < _currentOrders.Count ? _currentOrders[idx] : null;
    }

    private void ShowSelectedOrderDetail()
    {
        var order = GetSelectedOrder();
        if (order is null) return;

        using var dlg = new OrderDetailDialog(order, _settings);
        dlg.ShowDialog(FindForm());
    }

    private async Task CancelSelectedOrderAsync()
    {
        var user = _appSession.CurrentUser;
        var order = GetSelectedOrder();
        if (user is null || order is null) return;

        if (order.Status == OrderStatus.Cancelled)
        {
            MessageBox.Show("Đơn hàng này đã bị hủy trước đó.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var reason = PromptHelper.ShowTextInput(
            FindForm()!,
            $"Hủy đơn {order.OrderCode}",
            "Nhập lý do hủy đơn (sẽ hoàn kho nguyên liệu tự động):",
            required: true);
        if (string.IsNullOrWhiteSpace(reason)) return;

        var res = await _orderService.CancelOrderAsync(order.Id, user.Id, reason);
        MessageBox.Show(res.Message, res.Success ? "Thành công" : "Lỗi", MessageBoxButtons.OK,
            res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Error);

        if (res.Success)
            await LoadAsync();
    }

    private void ExportCsv()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"DonHang_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

        _csvExportService.ExportOrders(sfd.FileName, _currentOrders);
        MessageBox.Show("Xuất file CSV thành công!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
