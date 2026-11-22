using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class EndShiftDialog : Form
{
    private readonly IShiftService _shiftService;
    private readonly int _userId;
    private readonly EndShiftSummaryDto _summary;
    private readonly NumericUpDown _numEndCash = new();
    private readonly TextBox _txtNote = new();
    private readonly Label _lblVariance = new();
    private readonly Button _btnConfirm;

    public EndShiftDialog(IShiftService shiftService, int userId, EndShiftSummaryDto summary)
    {
        _shiftService = shiftService;
        _userId = userId;
        _summary = summary;

        Text = $"Chốt ca #{summary.SessionId}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 420);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        var infoBox = new Panel
        {
            Location = new Point(20, 16),
            Size = new Size(420, 152),
            BackColor = CoffeeTheme.Cream,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12)
        };

        infoBox.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = CoffeeTheme.BodyFont,
            Text =
                $"Giờ vào ca: {summary.StartTime:dd/MM/yyyy HH:mm} ({summary.OrderCount} đơn)\n" +
                $"Tiền đầu ca: {FormatHelper.Currency(summary.StartCash)}\n" +
                $"Doanh thu tiền mặt: {FormatHelper.Currency(summary.CashSales)}\n" +
                $"Doanh thu chuyển khoản: {FormatHelper.Currency(summary.TransferSales)}\n" +
                $"Admin đã rút trong ca: {FormatHelper.Currency(summary.CashWithdrawn)}\n" +
                $"Tiền mặt kỳ vọng trong két: {FormatHelper.Currency(summary.ExpectedCash)}"
        });

        var lblCash = new Label
        {
            Text = "Tiền mặt thực tế đếm được (VNĐ):",
            Font = CoffeeTheme.BodyBoldFont,
            Location = new Point(20, 182),
            Size = new Size(420, 22)
        };

        _numEndCash.Location = new Point(20, 206);
        _numEndCash.Size = new Size(420, 32);
        _numEndCash.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _numEndCash.Maximum = 1_000_000_000m;
        _numEndCash.ThousandsSeparator = true;
        _numEndCash.Value = Math.Clamp(summary.ExpectedCash, _numEndCash.Minimum, _numEndCash.Maximum);
        _numEndCash.ValueChanged += (_, _) => UpdateVarianceLabel();

        _lblVariance.Location = new Point(20, 242);
        _lblVariance.Size = new Size(420, 22);
        _lblVariance.Font = CoffeeTheme.BodyBoldFont;
        UpdateVarianceLabel();

        var lblNote = new Label
        {
            Text = "Ghi chú bàn giao / chênh lệch:",
            Font = CoffeeTheme.BodyBoldFont,
            Location = new Point(20, 272),
            Size = new Size(420, 22)
        };

        _txtNote.Location = new Point(20, 296);
        _txtNote.Size = new Size(420, 56);
        _txtNote.Multiline = true;

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Hủy", 120, 38);
        btnCancel.Location = new Point(176, 366);
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _btnConfirm = CoffeeTheme.CreateDangerButton("Xác nhận chốt ca", 136, 38);
        _btnConfirm.Location = new Point(304, 366);
        _btnConfirm.Click += async (_, _) => await ConfirmEndAsync();

        Controls.AddRange(new Control[]
        {
            infoBox, lblCash, _numEndCash, _lblVariance, lblNote, _txtNote, btnCancel, _btnConfirm
        });
    }

    private void UpdateVarianceLabel()
    {
        var diff = _numEndCash.Value - _summary.ExpectedCash;
        if (diff == 0)
        {
            _lblVariance.ForeColor = CoffeeTheme.AccentGreen;
            _lblVariance.Text = "Chênh lệch két: Khớp (0 đ)";
        }
        else if (diff > 0)
        {
            _lblVariance.ForeColor = CoffeeTheme.AccentBlue;
            _lblVariance.Text = $"Chênh lệch két: Dư +{FormatHelper.Currency(diff)}";
        }
        else
        {
            _lblVariance.ForeColor = CoffeeTheme.DangerRed;
            _lblVariance.Text = $"Chênh lệch két: Thiếu {FormatHelper.Currency(diff)}";
        }
    }

    private async Task ConfirmEndAsync()
    {
        _btnConfirm.Enabled = false;
        var res = await _shiftService.EndShiftAsync(_userId, _numEndCash.Value, _txtNote.Text);
        if (!res.Success)
        {
            MessageBox.Show(res.Message, "Lỗi chốt ca", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnConfirm.Enabled = true;
            return;
        }

        MessageBox.Show(res.Message, "Chốt ca thành công", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
