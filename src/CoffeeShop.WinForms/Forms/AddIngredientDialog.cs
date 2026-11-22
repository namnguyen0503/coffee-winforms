using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class AddIngredientDialog : Form
{
    private readonly IInventoryService _inventoryService;
    private readonly int _userId;

    private readonly TextBox _txtName = new();
    private readonly TextBox _txtUnit = new();
    private readonly NumericUpDown _numInitQty = new();
    private readonly NumericUpDown _numMinQty = new();
    private readonly NumericUpDown _numInitCost = new();
    private readonly Button _btnSave;

    public AddIngredientDialog(IInventoryService inventoryService, int userId)
    {
        _inventoryService = inventoryService;
        _userId = userId;

        Text = "Thêm nguyên liệu mới";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 360);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        AddField("Tên nguyên liệu *", _txtName, 16);
        AddField("Đơn vị tính (g, ml, túi, quả...) *", _txtUnit, 76);

        _numInitQty.DecimalPlaces = 2;
        _numInitQty.Maximum = 1_000_000m;
        AddField("Số lượng tồn ban đầu", _numInitQty, 136);

        _numMinQty.DecimalPlaces = 2;
        _numMinQty.Maximum = 1_000_000m;
        _numMinQty.Value = 100m;
        AddField("Ngưỡng cảnh báo sắp hết", _numMinQty, 196);

        _numInitCost.Maximum = 1_000_000_000m;
        _numInitCost.ThousandsSeparator = true;
        AddField("Tổng tiền vốn nhập ban đầu (VNĐ)", _numInitCost, 256);

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Hủy", 100, 36);
        btnCancel.Location = new Point(188, 310);
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        _btnSave = CoffeeTheme.CreateSuccessButton("Lưu nguyên liệu", 120, 36);
        _btnSave.Location = new Point(296, 310);
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
        _btnSave.Enabled = false;
        var res = await _inventoryService.AddIngredientAsync(
            new AddIngredientRequest(_txtName.Text, _txtUnit.Text, _numInitQty.Value, _numMinQty.Value, _numInitCost.Value),
            _userId);

        if (!res.Success)
        {
            MessageBox.Show(res.Message, "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnSave.Enabled = true;
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
