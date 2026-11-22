using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class ExportStockDialog : Form
{
    private readonly IInventoryService _inventoryService;
    private readonly int _userId;
    private readonly ComboBox _cboIng = new();
    private readonly NumericUpDown _numQty = new();
    private readonly TextBox _txtReason = new();
    private readonly Button _btnSave;

    public ExportStockDialog(IInventoryService inventoryService, int userId, IReadOnlyList<IngredientDto> ingredients, int? selectedId)
    {
        _inventoryService = inventoryService;
        _userId = userId;

        Text = "Xuất kho thủ công / Xuất hủy";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 270);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        _cboIng.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboIng.DisplayMember = "Display";
        _cboIng.ValueMember = "Id";
        _cboIng.DataSource = ingredients.Select(i => new { i.Id, Display = $"{i.Name} (Tồn: {i.Quantity:0.##} {i.Unit})" }).ToList();
        if (selectedId.HasValue && ingredients.Any(i => i.Id == selectedId.Value))
            _cboIng.SelectedValue = selectedId.Value;

        AddField("Nguyên liệu *", _cboIng, 16);

        _numQty.DecimalPlaces = 2;
        _numQty.Minimum = 0.01m;
        _numQty.Maximum = 1_000_000m;
        _numQty.Value = 10m;
        AddField("Số lượng xuất *", _numQty, 76);

        AddField("Lý do xuất (VD: Hư hỏng, Kiểm kê hao hụt) *", _txtReason, 136);

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Hủy", 100, 36);
        btnCancel.Location = new Point(188, 214);
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        _btnSave = CoffeeTheme.CreateDangerButton("Xác nhận xuất", 120, 36);
        _btnSave.Location = new Point(296, 214);
        _btnSave.Click += async (_, _) => await SaveAsync();

        Controls.AddRange(new Control[] { btnCancel, _btnSave });
        AcceptButton = _btnSave;
    }

    private void AddField(string label, Control input, int y)
    {
        Controls.Add(new Label { Text = label, Font = CoffeeTheme.BodyBoldFont, Location = new Point(20, y), Size = new Size(380, 20) });
        input.Location = new Point(20, y + 22);
        input.Size = new Size(380, 28);
        Controls.Add(input);
    }

    private async Task SaveAsync()
    {
        if (_cboIng.SelectedValue is not int ingId)
            return;

        _btnSave.Enabled = false;
        var res = await _inventoryService.ManualExportAsync(
            new ManualExportRequest(ingId, _numQty.Value, _txtReason.Text),
            _userId);

        if (!res.Success)
        {
            MessageBox.Show(res.Message, "Lỗi xuất kho", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnSave.Enabled = true;
            return;
        }

        MessageBox.Show(res.Message, "Thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
