using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Forms;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Controls;

public class WarehouseControl : UserControl
{
    private readonly IInventoryService _inventoryService;
    private readonly IUserScheduleService _userService;
    private readonly ICsvExportService _csvExportService;
    private readonly AppSession _appSession;

    private readonly TextBox _txtSearch = new();
    private readonly CheckBox _chkLowStockOnly = new();
    private readonly DataGridView _stockGrid = new();

    private readonly DateTimePicker _dtStart = new();
    private readonly DateTimePicker _dtEnd = new();
    private readonly ComboBox _cboFilterIng = new();
    private readonly ComboBox _cboFilterType = new();
    private readonly DataGridView _logGrid = new();

    private List<IngredientDto> _ingredients = new();
    private List<InventoryTransactionDto> _logs = new();

    public WarehouseControl(
        IInventoryService inventoryService,
        IUserScheduleService userService,
        ICsvExportService csvExportService,
        AppSession appSession)
    {
        _inventoryService = inventoryService;
        _userService = userService;
        _csvExportService = csvExportService;
        _appSession = appSession;

        Dock = DockStyle.Fill;
        BackColor = CoffeeTheme.Cream;
        Padding = new Padding(16);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = CoffeeTheme.BodyBoldFont };
        var tabStock = new TabPage("Tồn kho nguyên liệu") { BackColor = CoffeeTheme.Cream, Padding = new Padding(8) };
        var tabLogs = new TabPage("Lịch sử Nhập / Xuất kho") { BackColor = CoffeeTheme.Cream, Padding = new Padding(8) };

        BuildStockTab(tabStock);
        BuildLogsTab(tabLogs);

        tabs.TabPages.Add(tabStock);
        tabs.TabPages.Add(tabLogs);
        tabs.SelectedIndexChanged += async (_, _) =>
        {
            if (tabs.SelectedIndex == 1) await LoadLogsAsync();
        };

        Controls.Add(tabs);
    }

    private void BuildStockTab(TabPage tab)
    {
        var bar = CoffeeTheme.CreateCardPanel(new Padding(10));
        bar.Dock = DockStyle.Top;
        bar.Height = 60;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _txtSearch.Width = 200;
        _txtSearch.PlaceholderText = "Tìm nguyên liệu...";
        _txtSearch.TextChanged += async (_, _) => await LoadStockAsync();

        _chkLowStockOnly.Text = "Chỉ hiện sắp hết";
        _chkLowStockOnly.AutoSize = true;
        _chkLowStockOnly.Padding = new Padding(6, 6, 10, 0);
        _chkLowStockOnly.CheckedChanged += async (_, _) => await LoadStockAsync();

        var btnAdd = CoffeeTheme.CreateSuccessButton("+ Thêm NL mới", 130, 32);
        btnAdd.Click += async (_, _) => await OpenAddIngredientAsync();

        var btnImport = CoffeeTheme.CreatePrimaryButton("Nhập kho", 110, 32);
        btnImport.Click += async (_, _) => await OpenImportAsync();

        var btnExport = CoffeeTheme.CreateDangerButton("Xuất kho", 110, 32);
        btnExport.Click += async (_, _) => await OpenExportAsync();

        var btnSaveMin = CoffeeTheme.CreateSecondaryButton("Lưu ngưỡng tối thiểu", 160, 32);
        btnSaveMin.Click += async (_, _) => await SaveMinStocksAsync();

        flow.Controls.AddRange(new Control[]
        {
            _txtSearch, _chkLowStockOnly, btnAdd, btnImport, btnExport, btnSaveMin
        });
        bar.Controls.Add(flow);

        var gridCard = CoffeeTheme.CreateCardPanel();
        gridCard.Dock = DockStyle.Fill;
        CoffeeTheme.StyleDataGridView(_stockGrid);
        _stockGrid.Dock = DockStyle.Fill;
        _stockGrid.ReadOnly = false;

        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Id", HeaderText = "ID", ReadOnly = true, FillWeight = 35 });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Name", HeaderText = "Tên nguyên liệu", ReadOnly = true });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Unit", HeaderText = "ĐVT", ReadOnly = true, FillWeight = 50 });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Quantity", HeaderText = "Tồn hiện tại", ReadOnly = true, FillWeight = 75 });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "MinQuantity", HeaderText = "Ngưỡng cảnh báo (Sửa được)", ReadOnly = false, FillWeight = 85 });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "AvgCost", HeaderText = "Giá vốn BQ", ReadOnly = true, FillWeight = 75 });
        _stockGrid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Status", HeaderText = "Trạng thái", ReadOnly = true, FillWeight = 75 });

        gridCard.Controls.Add(_stockGrid);
        tab.Controls.Add(gridCard);
        tab.Controls.Add(bar);
    }

    private void BuildLogsTab(TabPage tab)
    {
        var bar = CoffeeTheme.CreateCardPanel(new Padding(10));
        bar.Dock = DockStyle.Top;
        bar.Height = 60;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _dtStart.Format = DateTimePickerFormat.Short;
        _dtStart.Width = 110;
        _dtStart.Value = DateTime.Today.AddDays(-30);

        _dtEnd.Format = DateTimePickerFormat.Short;
        _dtEnd.Width = 110;
        _dtEnd.Value = DateTime.Today;

        _cboFilterIng.Width = 165;
        _cboFilterIng.DropDownStyle = ComboBoxStyle.DropDownList;

        _cboFilterType.Width = 150;
        _cboFilterType.DropDownStyle = ComboBoxStyle.DropDownList;
        var typeItems = new List<KeyValuePair<string, InventoryTransactionType?>>
        {
            new("Tất cả loại", null),
            new("Nhập kho", InventoryTransactionType.Import),
            new("Xuất bán hàng", InventoryTransactionType.SaleExport),
            new("Xuất điều chỉnh", InventoryTransactionType.ManualExport),
            new("Hoàn kho hủy đơn", InventoryTransactionType.CancelRestock)
        };
        _cboFilterType.DisplayMember = "Key";
        _cboFilterType.ValueMember = "Value";
        _cboFilterType.DataSource = typeItems;

        var btnSearch = CoffeeTheme.CreatePrimaryButton("Lọc lịch sử", 105, 32);
        btnSearch.Click += async (_, _) => await LoadLogsAsync();

        var btnExportCsv = CoffeeTheme.CreateSecondaryButton("Xuất CSV", 100, 32);
        btnExportCsv.Click += (_, _) => ExportLogsCsv();

        flow.Controls.AddRange(new Control[]
        {
            new Label { Text = "Từ:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) },
            _dtStart,
            new Label { Text = "Đến:", AutoSize = true, Padding = new Padding(6, 8, 4, 0) },
            _dtEnd,
            _cboFilterIng,
            _cboFilterType,
            btnSearch,
            btnExportCsv
        });
        bar.Controls.Add(flow);

        var gridCard = CoffeeTheme.CreateCardPanel();
        gridCard.Dock = DockStyle.Fill;
        CoffeeTheme.StyleDataGridView(_logGrid);
        _logGrid.Dock = DockStyle.Fill;
        _logGrid.ReadOnly = true;

        _logGrid.Columns.Add("CreatedAt", "Thời gian");
        _logGrid.Columns.Add("Ingredient", "Nguyên liệu");
        _logGrid.Columns.Add("Type", "Loại giao dịch");
        _logGrid.Columns.Add("Qty", "Số lượng");
        _logGrid.Columns.Add("TotalCost", "Chi phí / Giá trị");
        _logGrid.Columns.Add("User", "Người thực hiện");
        _logGrid.Columns.Add("Note", "Ghi chú");

        gridCard.Controls.Add(_logGrid);
        tab.Controls.Add(gridCard);
        tab.Controls.Add(bar);
    }

    public async Task LoadAsync()
    {
        if (IsDisposed) return;
        await LoadStockAsync();
        if (IsDisposed) return;
        PopulateLogFilters();
        await LoadLogsAsync();
    }

    private async Task LoadStockAsync()
    {
        if (IsDisposed) return;
        _ingredients = (await _inventoryService.GetIngredientsAsync(_txtSearch.Text, _chkLowStockOnly.Checked)).ToList();
        if (IsDisposed) return;

        _stockGrid.Rows.Clear();
        foreach (var i in _ingredients)
        {
            var idx = _stockGrid.Rows.Add(
                i.Id,
                i.Name,
                i.Unit,
                $"{i.Quantity:0.###}",
                i.MinQuantity.ToString("0.###"),
                FormatHelper.Currency(i.AverageUnitCost),
                i.IsLowStock ? "SẮP HẾT!" : "Đủ dùng");

            if (i.IsLowStock)
                _stockGrid.Rows[idx].DefaultCellStyle.ForeColor = CoffeeTheme.DangerRed;
        }
    }

    private void PopulateLogFilters()
    {
        var ingFilter = new List<KeyValuePair<string, int?>> { new("Tất cả nguyên liệu", null) };
        ingFilter.AddRange(_ingredients.Select(i => new KeyValuePair<string, int?>(i.Name, i.Id)));
        _cboFilterIng.DisplayMember = "Key";
        _cboFilterIng.ValueMember = "Value";
        _cboFilterIng.DataSource = ingFilter;
    }

    private async Task LoadLogsAsync()
    {
        if (IsDisposed) return;

        var selectedIng = _cboFilterIng.SelectedItem is KeyValuePair<string, int?> ingKv
            ? ingKv.Value
            : _cboFilterIng.SelectedValue as int?;
        var selectedType = _cboFilterType.SelectedItem is KeyValuePair<string, InventoryTransactionType?> typeKv
            ? typeKv.Value
            : _cboFilterType.SelectedValue as InventoryTransactionType?;

        var filter = new InventoryLogFilterRequest(
            DateOnly.FromDateTime(_dtStart.Value),
            DateOnly.FromDateTime(_dtEnd.Value),
            selectedIng,
            selectedType,
            null,
            1,
            250);

        _logs = (await _inventoryService.GetTransactionsAsync(filter)).ToList();
        if (IsDisposed) return;
        _logGrid.Rows.Clear();
        foreach (var l in _logs)
        {
            _logGrid.Rows.Add(
                l.CreatedAt.ToString("dd/MM/yyyy HH:mm"),
                l.IngredientName,
                FormatHelper.InventoryTxLabel(l.Type),
                $"{l.Quantity:0.###} {l.Unit}",
                l.TotalCost > 0 ? FormatHelper.Currency(l.TotalCost) : "-",
                l.PerformedBy,
                l.Note ?? string.Empty);
        }
    }

    private int? GetSelectedIngredientId()
    {
        if (_stockGrid.CurrentRow is null) return null;
        var val = _stockGrid.CurrentRow.Cells["Id"].Value;
        return val is int id ? id : null;
    }

    private async Task OpenAddIngredientAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is null) return;
        using var dlg = new AddIngredientDialog(_inventoryService, user.Id);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            await LoadAsync();
    }

    private async Task OpenImportAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is null) return;
        using var dlg = new ImportStockDialog(_inventoryService, user.Id, _ingredients, GetSelectedIngredientId());
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            await LoadAsync();
    }

    private async Task OpenExportAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is null) return;
        using var dlg = new ExportStockDialog(_inventoryService, user.Id, _ingredients, GetSelectedIngredientId());
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            await LoadAsync();
    }

    private async Task SaveMinStocksAsync()
    {
        var updates = new List<MinStockUpdateItem>();
        foreach (DataGridViewRow row in _stockGrid.Rows)
        {
            if (row.Cells["Id"].Value is int id &&
                decimal.TryParse(row.Cells["MinQuantity"].Value?.ToString(), out var minVal))
            {
                updates.Add(new MinStockUpdateItem(id, minVal));
            }
        }

        var res = await _inventoryService.UpdateMinStocksAsync(updates);
        MessageBox.Show(res.Message, res.Success ? "Thành công" : "Lỗi", MessageBoxButtons.OK,
            res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);
        if (res.Success)
            await LoadStockAsync();
    }

    private void ExportLogsCsv()
    {
        using var sfd = new SaveFileDialog
        {
            Filter = "CSV file (*.csv)|*.csv",
            FileName = $"LichSuKho_{DateTime.Now:yyyyMMdd_HHmm}.csv"
        };
        if (sfd.ShowDialog(FindForm()) != DialogResult.OK) return;

        _csvExportService.ExportInventoryLogs(sfd.FileName, _logs);
        MessageBox.Show("Xuất lịch sử kho CSV thành công!", "Hoàn tất", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }
}
