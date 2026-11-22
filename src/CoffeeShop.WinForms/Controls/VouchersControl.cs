using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.WinForms.Forms;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Controls;

public class VouchersControl : UserControl
{
    private readonly IVoucherService _voucherService;
    private readonly DataGridView _grid = new();
    private List<VoucherDto> _vouchers = new();

    public VouchersControl(IVoucherService voucherService)
    {
        _voucherService = voucherService;
        Dock = DockStyle.Fill;
        BackColor = CoffeeTheme.Cream;
        Padding = new Padding(16);

        var topPanel = CoffeeTheme.CreateCardPanel(new Padding(10));
        topPanel.Dock = DockStyle.Top;
        topPanel.Height = 58;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        var btnAdd = CoffeeTheme.CreateSuccessButton("+ Thêm mã giảm giá", 150, 32);
        btnAdd.Click += async (_, _) => await AddAsync();

        var btnToggle = CoffeeTheme.CreatePrimaryButton("Bật / Tắt mã", 130, 32);
        btnToggle.Click += async (_, _) => await ToggleSelectedAsync();

        var btnDelete = CoffeeTheme.CreateDangerButton("Xóa mã", 100, 32);
        btnDelete.Click += async (_, _) => await DeleteSelectedAsync();

        flow.Controls.AddRange(new Control[] { btnAdd, btnToggle, btnDelete });
        topPanel.Controls.Add(flow);

        var gridCard = CoffeeTheme.CreateCardPanel();
        gridCard.Dock = DockStyle.Fill;
        CoffeeTheme.StyleDataGridView(_grid);
        _grid.Dock = DockStyle.Fill;
        _grid.ReadOnly = true;

        _grid.Columns.Add("Code", "Mã Voucher");
        _grid.Columns.Add("Discount", "Giảm (%)");
        _grid.Columns.Add("MinOrder", "Đơn tối thiểu");
        _grid.Columns.Add("ExpiresAt", "Hạn sử dụng");
        _grid.Columns.Add("Status", "Trạng thái");
        _grid.Columns.Add("Description", "Mô tả");

        gridCard.Controls.Add(_grid);
        Controls.Add(gridCard);
        Controls.Add(topPanel);
    }

    public async Task LoadAsync()
    {
        _vouchers = (await _voucherService.GetAllAsync()).ToList();
        _grid.Rows.Clear();
        foreach (var v in _vouchers)
        {
            var idx = _grid.Rows.Add(
                v.Code,
                $"{v.DiscountPercent:0.##}%",
                FormatHelper.Currency(v.MinOrderAmount),
                v.ExpiresAt?.ToString("dd/MM/yyyy") ?? "Không giới hạn",
                v.IsActive ? "Đang hoạt động" : "Tạm khóa",
                v.Description ?? string.Empty);

            if (!v.IsActive)
                _grid.Rows[idx].DefaultCellStyle.ForeColor = CoffeeTheme.TextMuted;
        }
    }

    private VoucherDto? GetSelected()
    {
        if (_grid.CurrentRow is null) return null;
        var idx = _grid.CurrentRow.Index;
        return idx >= 0 && idx < _vouchers.Count ? _vouchers[idx] : null;
    }

    private async Task AddAsync()
    {
        using var dlg = new AddVoucherDialog(_voucherService);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            await LoadAsync();
    }

    private async Task ToggleSelectedAsync()
    {
        var v = GetSelected();
        if (v is null) return;
        await _voucherService.ToggleActiveAsync(v.Id);
        await LoadAsync();
    }

    private async Task DeleteSelectedAsync()
    {
        var v = GetSelected();
        if (v is null) return;
        if (MessageBox.Show($"Xóa voucher '{v.Code}'?", "Xác nhận", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
            return;
        await _voucherService.DeleteAsync(v.Id);
        await LoadAsync();
    }
}
