using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Controls;

public class ReportsControl : UserControl
{
    private readonly IReportService _reportService;
    private readonly IShiftService _shiftService;
    private readonly ICsvExportService _csvExportService;
    private readonly AppSession? _appSession;
    private readonly DataGridView _withdrawalGrid = new();

    private readonly DateTimePicker _dtRepStart = new();
    private readonly DateTimePicker _dtRepEnd = new();
    private readonly Label _lblRev;
    private readonly Label _lblCogs;
    private readonly Label _lblProfit;
    private readonly Label _lblMargin;
    private readonly DataGridView _orderProfitGrid = new();
    private readonly DataGridView _topProductGrid = new();

    private readonly DateTimePicker _dtCfStart = new();
    private readonly DateTimePicker _dtCfEnd = new();
    private readonly CheckBox _chkDiffOnly = new();
    private readonly DataGridView _cashFlowGrid = new();

    private FinancialReportDto? _lastReport;
    private List<CashFlowRowDto> _lastCashFlow = new();

    public ReportsControl(
        IReportService reportService,
        IShiftService shiftService,
        ICsvExportService csvExportService,
        AppSession? appSession = null)
    {
        _reportService = reportService;
        _shiftService = shiftService;
        _csvExportService = csvExportService;
        _appSession = appSession;

        Dock = DockStyle.Fill;
        BackColor = CoffeeTheme.Cream;
        Padding = new Padding(16);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = CoffeeTheme.BodyBoldFont };
        var tabFinancial = new TabPage("Doanh thu & Lợi nhuận") { BackColor = CoffeeTheme.Cream, Padding = new Padding(8) };
        var tabCashFlow = new TabPage("Đối soát Dòng tiền / Ca làm việc") { BackColor = CoffeeTheme.Cream, Padding = new Padding(8) };

        BuildFinancialTab(tabFinancial, out _lblRev, out _lblCogs, out _lblProfit, out _lblMargin);
        BuildCashFlowTab(tabCashFlow);

        tabs.TabPages.Add(tabFinancial);
        tabs.TabPages.Add(tabCashFlow);
        Controls.Add(tabs);
    }

    private void BuildFinancialTab(
        TabPage tab,
        out Label lblRev,
        out Label lblCogs,
        out Label lblProfit,
        out Label lblMargin)
    {
        var root = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 3, ColumnCount = 1 };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 58));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 106));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var bar = CoffeeTheme.CreateCardPanel(new Padding(10));
        bar.Dock = DockStyle.Fill;
        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _dtRepStart.Format = DateTimePickerFormat.Short;
        _dtRepStart.Width = 115;
        _dtRepStart.Value = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
        _dtRepEnd.Format = DateTimePickerFormat.Short;
        _dtRepEnd.Width = 115;
        _dtRepEnd.Value = DateTime.Today;

        var btnFilter = CoffeeTheme.CreatePrimaryButton("Xem báo cáo", 120, 32);
        btnFilter.Click += async (_, _) => await LoadFinancialAsync();

        var btnExport = CoffeeTheme.CreateSecondaryButton("Xuất CSV", 100, 32);
        btnExport.Click += (_, _) => ExportFinancialCsv();

        flow.Controls.AddRange(new Control[]
        {
            new Label { Text = "Từ ngày:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) },
            _dtRepStart,
            new Label { Text = "Đến ngày:", AutoSize = true, Padding = new Padding(8, 8, 4, 0) },
            _dtRepEnd,
            btnFilter,
            btnExport
        });
        bar.Controls.Add(flow);

        var kpis = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 4, RowCount = 1 };
        for (int i = 0; i < 4; i++)
            kpis.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25));

        kpis.Controls.Add(CoffeeTheme.CreateKpiCard("Tổng doanh thu", "0 đ", CoffeeTheme.AccentBlue, out lblRev), 0, 0);
        kpis.Controls.Add(CoffeeTheme.CreateKpiCard("Tổng giá vốn (COGS)", "0 đ", CoffeeTheme.WarningOrange, out lblCogs), 1, 0);
        kpis.Controls.Add(CoffeeTheme.CreateKpiCard("Lợi nhuận gộp", "0 đ", CoffeeTheme.AccentGreen, out lblProfit), 2, 0);
        kpis.Controls.Add(CoffeeTheme.CreateKpiCard("Biên lợi nhuận", "0 %", CoffeeTheme.Mocha, out lblMargin), 3, 0);

        var split = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1 };
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 62));
        split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 38));

        var leftCard = CoffeeTheme.CreateCardPanel();
        leftCard.Dock = DockStyle.Fill;
        leftCard.Margin = new Padding(4);
        leftCard.Controls.Add(_orderProfitGrid);
        leftCard.Controls.Add(new Label { Text = "Chi tiết Lợi nhuận theo Đơn hàng", Font = CoffeeTheme.SectionFont, Dock = DockStyle.Top, Height = 28 });

        CoffeeTheme.StyleDataGridView(_orderProfitGrid);
        _orderProfitGrid.Dock = DockStyle.Fill;
        _orderProfitGrid.ReadOnly = true;
        _orderProfitGrid.Columns.Add("OrderCode", "Mã đơn");
        _orderProfitGrid.Columns.Add("Date", "Thời gian");
        _orderProfitGrid.Columns.Add("Staff", "Thu ngân");
        _orderProfitGrid.Columns.Add("Rev", "Doanh thu");
        _orderProfitGrid.Columns.Add("Cogs", "Giá vốn");
        _orderProfitGrid.Columns.Add("Profit", "Lợi nhuận");

        var rightCard = CoffeeTheme.CreateCardPanel();
        rightCard.Dock = DockStyle.Fill;
        rightCard.Margin = new Padding(4);
        rightCard.Controls.Add(_topProductGrid);
        rightCard.Controls.Add(new Label { Text = "Top món bán chạy", Font = CoffeeTheme.SectionFont, Dock = DockStyle.Top, Height = 28 });

        CoffeeTheme.StyleDataGridView(_topProductGrid);
        _topProductGrid.Dock = DockStyle.Fill;
        _topProductGrid.ReadOnly = true;
        _topProductGrid.Columns.Add("Product", "Tên món");
        _topProductGrid.Columns.Add("Qty", "SL bán");
        _topProductGrid.Columns.Add("Rev", "Doanh thu");
        _topProductGrid.Columns.Add("Profit", "Lãi gộp");
        _topProductGrid.Columns["Qty"]!.FillWeight = 45;

        split.Controls.Add(leftCard, 0, 0);
        split.Controls.Add(rightCard, 1, 0);

        root.Controls.Add(bar, 0, 0);
        root.Controls.Add(kpis, 0, 1);
        root.Controls.Add(split, 0, 2);
        tab.Controls.Add(root);
    }

    private void BuildCashFlowTab(TabPage tab)
    {
        var bar = CoffeeTheme.CreateCardPanel(new Padding(10));
        bar.Dock = DockStyle.Top;
        bar.Height = 58;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _dtCfStart.Format = DateTimePickerFormat.Short;
        _dtCfStart.Width = 115;
        _dtCfStart.Value = DateTime.Today.AddDays(-14);

        _dtCfEnd.Format = DateTimePickerFormat.Short;
        _dtCfEnd.Width = 115;
        _dtCfEnd.Value = DateTime.Today;

        _chkDiffOnly.Text = "Chỉ hiện ca có lệch tiền";
        _chkDiffOnly.AutoSize = true;
        _chkDiffOnly.Padding = new Padding(8, 6, 10, 0);
        _chkDiffOnly.CheckedChanged += async (_, _) => await LoadCashFlowAsync();

        var btnFilter = CoffeeTheme.CreatePrimaryButton("Đối soát", 100, 32);
        btnFilter.Click += async (_, _) => await LoadCashFlowAsync();

        var btnExport = CoffeeTheme.CreateSecondaryButton("Xuất CSV", 100, 32);
        btnExport.Click += (_, _) => ExportCashFlowCsv();

        flow.Controls.AddRange(new Control[]
        {
            new Label { Text = "Từ:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) },
            _dtCfStart,
            new Label { Text = "Đến:", AutoSize = true, Padding = new Padding(6, 8, 4, 0) },
            _dtCfEnd,
            _chkDiffOnly,
            btnFilter,
            btnExport
        });

        if (_appSession?.IsAdmin == true)
        {
            var btnWithdraw = CoffeeTheme.CreateDangerButton("Admin rút tiền két", 165, 34);
            btnWithdraw.Click += async (_, _) => await WithdrawCashAsync();
            flow.Controls.Add(btnWithdraw);
        }
        bar.Controls.Add(flow);

        var gridCard = CoffeeTheme.CreateCardPanel();
        gridCard.Dock = DockStyle.Fill;
        CoffeeTheme.StyleDataGridView(_cashFlowGrid);
        _cashFlowGrid.Dock = DockStyle.Fill;
        _cashFlowGrid.ReadOnly = true;

        _cashFlowGrid.Columns.Add("SessionId", "Ca #");
        _cashFlowGrid.Columns.Add("Staff", "Nhân viên");
        _cashFlowGrid.Columns.Add("Start", "Vào ca");
        _cashFlowGrid.Columns.Add("End", "Chốt ca");
        _cashFlowGrid.Columns.Add("StartCash", "Đầu ca");
        _cashFlowGrid.Columns.Add("HandoverDiff", "Lệch bàn giao");
        _cashFlowGrid.Columns.Add("CashSales", "TM bán hàng");
        _cashFlowGrid.Columns.Add("TransferSales", "CK bán hàng");
        _cashFlowGrid.Columns.Add("CashWithdrawn", "Admin rút");
        _cashFlowGrid.Columns.Add("Expected", "Kỳ vọng két");
        _cashFlowGrid.Columns.Add("EndCash", "Thực đếm");
        _cashFlowGrid.Columns.Add("Variance", "Lệch kiểm kê");
        _cashFlowGrid.Columns.Add("Note", "Ghi chú");
        _cashFlowGrid.Columns["SessionId"]!.FillWeight = 40;

        gridCard.Controls.Add(_cashFlowGrid);

        var auditCard = CoffeeTheme.CreateCardPanel();
        auditCard.Dock = DockStyle.Fill;
        var auditHeader = new Label
        {
            Text = "Lịch sử Admin rút tiền két (Audit Log)",
            Font = CoffeeTheme.BodyBoldFont,
            ForeColor = CoffeeTheme.Espresso,
            Dock = DockStyle.Top,
            Height = 26
        };
        CoffeeTheme.StyleDataGridView(_withdrawalGrid);
        _withdrawalGrid.Dock = DockStyle.Fill;
        _withdrawalGrid.ReadOnly = true;
        _withdrawalGrid.Columns.Add("CreatedAt", "Thời gian");
        _withdrawalGrid.Columns.Add("Admin", "Người rút");
        _withdrawalGrid.Columns.Add("SessionRef", "Ca #");
        _withdrawalGrid.Columns.Add("Amount", "Số tiền rút");
        _withdrawalGrid.Columns.Add("Reason", "Lý do rút tiền");
        _withdrawalGrid.Columns["SessionRef"]!.FillWeight = 45;
        _withdrawalGrid.Columns["Reason"]!.FillWeight = 180;
        auditCard.Controls.Add(_withdrawalGrid);
        auditCard.Controls.Add(auditHeader);

        var splitLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2
        };
        splitLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 62F));
        splitLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 38F));
        splitLayout.Controls.Add(gridCard, 0, 0);
        splitLayout.Controls.Add(auditCard, 0, 1);

        tab.Controls.Add(splitLayout);
        tab.Controls.Add(bar);
    }

    public async Task LoadAsync()
    {
        await LoadFinancialAsync();
        await LoadCashFlowAsync();
    }

    private async Task LoadFinancialAsync()
    {
        _lastReport = await _reportService.GetFinancialReportAsync(
            DateOnly.FromDateTime(_dtRepStart.Value),
            DateOnly.FromDateTime(_dtRepEnd.Value));

        _lblRev.Text = FormatHelper.Currency(_lastReport.TotalRevenue);
        _lblCogs.Text = FormatHelper.Currency(_lastReport.TotalCogs);
        _lblProfit.Text = FormatHelper.Currency(_lastReport.GrossProfit);
        _lblMargin.Text = $"{_lastReport.GrossMarginPercent:0.##} % ({_lastReport.TotalOrders} đơn)";

        _orderProfitGrid.Rows.Clear();
        foreach (var o in _lastReport.Orders)
        {
            _orderProfitGrid.Rows.Add(
                o.OrderCode,
                o.OrderDate.ToString("dd/MM HH:mm"),
                o.StaffName,
                FormatHelper.Currency(o.Revenue),
                FormatHelper.Currency(o.CostOfGoodsSold),
                FormatHelper.Currency(o.GrossProfit));
        }

        _topProductGrid.Rows.Clear();
        foreach (var p in _lastReport.TopProducts)
        {
            _topProductGrid.Rows.Add(
                p.ProductName,
                p.QuantitySold,
                FormatHelper.Currency(p.Revenue),
                FormatHelper.Currency(p.GrossProfit));
        }
    }

    private async Task LoadCashFlowAsync()
    {
        var startDate = DateOnly.FromDateTime(_dtCfStart.Value);
        var endDate = DateOnly.FromDateTime(_dtCfEnd.Value);

        _lastCashFlow = (await _shiftService.GetCashFlowAsync(
            startDate,
            endDate,
            _chkDiffOnly.Checked)).ToList();

        _cashFlowGrid.Rows.Clear();
        foreach (var r in _lastCashFlow)
        {
            var idx = _cashFlowGrid.Rows.Add(
                r.SessionId,
                r.StaffName,
                r.StartTime.ToString("dd/MM HH:mm"),
                r.EndTime?.ToString("dd/MM HH:mm") ?? "Đang mở",
                FormatHelper.Currency(r.StartCash),
                r.HandoverDiff == 0 ? "Khớp" : FormatHelper.Currency(r.HandoverDiff),
                FormatHelper.Currency(r.CashSales),
                FormatHelper.Currency(r.TransferSales),
                r.CashWithdrawn == 0 ? "-" : FormatHelper.Currency(r.CashWithdrawn),
                FormatHelper.Currency(r.ExpectedCash),
                r.Status == WorkSessionStatus.Open ? "-" : FormatHelper.Currency(r.EndCash),
                r.Status == WorkSessionStatus.Open ? "-" : (r.CashVariance == 0 ? "Khớp" : FormatHelper.Currency(r.CashVariance)),
                r.Note ?? string.Empty);

            if (r.HandoverDiff != 0 || (r.Status == WorkSessionStatus.Closed && r.CashVariance != 0))
                _cashFlowGrid.Rows[idx].DefaultCellStyle.ForeColor = CoffeeTheme.DangerRed;
        }

        var withdrawals = await _shiftService.GetCashTransactionsAsync(startDate, endDate);
        _withdrawalGrid.Rows.Clear();
        foreach (var w in withdrawals)
        {
            _withdrawalGrid.Rows.Add(
                w.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                w.StaffName,
                w.WorkSessionId.HasValue ? $"Ca #{w.WorkSessionId}" : "Giữa ca",
                FormatHelper.Currency(w.Amount),
                w.Reason);
        }
    }

    private async Task WithdrawCashAsync()
    {
        if (_appSession?.CurrentUser is null || !_appSession.IsAdmin)
        {
            MessageBox.Show("Chỉ Admin mới có quyền rút tiền từ két.", "Không đủ quyền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        string contextText;
        decimal availableCash;

        var active = await _shiftService.GetAnyActiveSessionAsync();
        if (active is not null)
        {
            var preview = await _shiftService.GetCurrentShiftPreviewAsync(active.UserId);
            availableCash = preview.Data?.ExpectedCash ?? active.StartCash;
            contextText = $"Đang rút trong Ca #{active.Id} ({active.StaffName})";
        }
        else
        {
            var allRows = await _shiftService.GetCashFlowAsync(
                new DateOnly(2000, 1, 1),
                DateOnly.FromDateTime(DateTime.Today));
            var lastClosed = allRows.FirstOrDefault(r => r.Status == WorkSessionStatus.Closed);
            if (lastClosed is null)
            {
                contextText = "Hiện chưa có ca mở và chưa có ca đã chốt nào.";
                availableCash = 0m;
            }
            else
            {
                var gapStart = lastClosed.EndTime ?? lastClosed.StartTime;
                var txns = await _shiftService.GetCashTransactionsAsync(
                    DateOnly.FromDateTime(gapStart),
                    DateOnly.FromDateTime(DateTime.Today));
                var betweenWithdrawn = txns
                    .Where(t => !t.WorkSessionId.HasValue && t.CreatedAt >= gapStart)
                    .Sum(t => t.Amount);

                availableCash = Math.Max(0m, lastClosed.EndCash - betweenWithdrawn);
                contextText = $"Rút giữa ca (sau Ca #{lastClosed.SessionId} - chốt {FormatHelper.Currency(lastClosed.EndCash)})";
            }
        }

        using var dlg = new CoffeeShop.WinForms.Forms.WithdrawCashDialog(contextText, availableCash);
        if (dlg.ShowDialog(FindForm()) != DialogResult.OK)
            return;

        var res = await _shiftService.WithdrawCashAsync(
            _appSession.CurrentUser.Id,
            new AdminWithdrawCashRequest(dlg.Amount, dlg.Reason));

        if (!res.Success)
        {
            MessageBox.Show(res.Message, "Không thể rút tiền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        MessageBox.Show(
            $"Đã rút {FormatHelper.Currency(dlg.Amount)} khỏi két và lưu Audit Log.",
            "Rút tiền thành công",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);

        await LoadCashFlowAsync();
    }

    private void ExportFinancialCsv()
    {
        if (_lastReport is null) return;
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"BaoCaoLoiNhuan_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

        _csvExportService.ExportFinancialReport(sfd.FileName, _lastReport);
        MessageBox.Show("Xuất báo cáo lợi nhuận CSV thành công!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private void ExportCashFlowCsv()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"DoiSoatDongTien_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

        _csvExportService.ExportCashFlow(sfd.FileName, _lastCashFlow);
        MessageBox.Show("Xuất đối soát dòng tiền CSV thành công!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
