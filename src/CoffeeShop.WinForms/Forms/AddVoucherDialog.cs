using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class AddVoucherDialog : Form
{
    private readonly IVoucherService _voucherService;
    private readonly TextBox _txtCode = new();
    private readonly NumericUpDown _numPercent = new();
    private readonly NumericUpDown _numMinOrder = new();
    private readonly CheckBox _chkHasExpiry = new();
    private readonly DateTimePicker _dtExpiry = new();
    private readonly TextBox _txtDesc = new();
    private readonly Button _btnSave;

    public AddVoucherDialog(IVoucherService voucherService)
    {
        _voucherService = voucherService;

        Text = "Thêm mã khuyến mãi";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 370);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        _txtCode.CharacterCasing = CharacterCasing.Upper;
        AddField("Mã Voucher *", _txtCode, 16);

        _numPercent.Minimum = 1m;
        _numPercent.Maximum = 100m;
        _numPercent.Value = 10m;
        AddField("Phần trăm giảm giá (1-100%) *", _numPercent, 76);

        _numMinOrder.Maximum = 100_000_000m;
        _numMinOrder.Increment = 10000m;
        _numMinOrder.ThousandsSeparator = true;
        AddField("Đơn tối thiểu áp dụng (VNĐ)", _numMinOrder, 136);

        _chkHasExpiry.Text = "Giới hạn ngày hết hạn";
        _chkHasExpiry.Location = new Point(20, 200);
        _chkHasExpiry.Size = new Size(170, 24);
        _chkHasExpiry.CheckedChanged += (_, _) => _dtExpiry.Enabled = _chkHasExpiry.Checked;

        _dtExpiry.Location = new Point(196, 198);
        _dtExpiry.Size = new Size(204, 28);
        _dtExpiry.Format = DateTimePickerFormat.Short;
        _dtExpiry.Enabled = false;
        _dtExpiry.Value = DateTime.Today.AddMonths(1);

        AddField("Mô tả", _txtDesc, 236);

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Hủy", 100, 36);
        btnCancel.Location = new Point(188, 316);
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        _btnSave = CoffeeTheme.CreateSuccessButton("Tạo mã", 120, 36);
        _btnSave.Location = new Point(296, 316);
        _btnSave.Click += async (_, _) => await SaveAsync();

        Controls.AddRange(new Control[] { _chkHasExpiry, _dtExpiry, btnCancel, _btnSave });
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
        DateTime? expiry = _chkHasExpiry.Checked ? _dtExpiry.Value.Date.AddDays(1).AddSeconds(-1) : null;
        var res = await _voucherService.CreateAsync(new CreateVoucherRequest(
            _txtCode.Text,
            _numPercent.Value,
            _numMinOrder.Value,
            _txtDesc.Text,
            expiry));

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
