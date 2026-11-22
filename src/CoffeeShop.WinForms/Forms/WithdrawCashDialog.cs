using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public sealed class WithdrawCashDialog : Form
{
    private readonly NumericUpDown _numAmount = new();
    private readonly TextBox _txtReason = new();
    private readonly decimal _availableCash;

    public decimal Amount => _numAmount.Value;
    public string Reason => _txtReason.Text.Trim();

    public WithdrawCashDialog(string contextText, decimal availableCash)
    {
        _availableCash = availableCash;

        Text = "Admin rút tiền khỏi két";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        StartPosition = FormStartPosition.CenterParent;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 340);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        var infoBox = new Panel
        {
            Location = new Point(20, 16),
            Size = new Size(420, 74),
            BackColor = CoffeeTheme.Cream,
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(12, 10, 12, 10)
        };
        infoBox.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = CoffeeTheme.BodyFont,
            Text =
                $"{contextText}\n" +
                $"Tiền mặt ước tính hiện có trong két: {FormatHelper.Currency(availableCash)}"
        });

        var lblAmount = new Label
        {
            Text = "Số tiền muốn rút (VNĐ):",
            Font = CoffeeTheme.BodyBoldFont,
            Location = new Point(20, 104),
            AutoSize = true
        };

        _numAmount.Location = new Point(20, 128);
        _numAmount.Size = new Size(420, 32);
        _numAmount.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _numAmount.Maximum = 1_000_000_000m;
        _numAmount.Increment = 50_000m;
        _numAmount.ThousandsSeparator = true;
        _numAmount.Value = 0m;

        var lblReason = new Label
        {
            Text = "Lý do rút tiền (bắt buộc để lưu Audit Log):",
            Font = CoffeeTheme.BodyBoldFont,
            Location = new Point(20, 170),
            AutoSize = true
        };

        _txtReason.Location = new Point(20, 194);
        _txtReason.Size = new Size(420, 72);
        _txtReason.Multiline = true;
        _txtReason.MaxLength = 500;
        _txtReason.PlaceholderText = "VD: Nộp tiền về két tổng, chi mua nguyên liệu gấp...";

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Hủy", 100, 38);
        btnCancel.Location = new Point(192, 282);
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        var btnConfirm = CoffeeTheme.CreateDangerButton("Xác nhận rút tiền", 140, 38);
        btnConfirm.Location = new Point(300, 282);
        btnConfirm.Click += (_, _) => ConfirmWithdraw();

        Controls.AddRange(new Control[]
        {
            infoBox, lblAmount, _numAmount, lblReason, _txtReason, btnCancel, btnConfirm
        });

        AcceptButton = btnConfirm;
        Shown += (_, _) =>
        {
            _numAmount.Focus();
            _numAmount.Select(0, _numAmount.Text.Length);
        };
    }

    private void ConfirmWithdraw()
    {
        if (_numAmount.Value <= 0)
        {
            MessageBox.Show("Số tiền rút phải lớn hơn 0.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _numAmount.Focus();
            return;
        }

        if (_numAmount.Value > _availableCash)
        {
            MessageBox.Show(
                $"Số tiền rút ({FormatHelper.Currency(_numAmount.Value)}) vượt quá tiền mặt hiện có trong két ({FormatHelper.Currency(_availableCash)}).",
                "Vượt số dư két",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            _numAmount.Focus();
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtReason.Text))
        {
            MessageBox.Show("Vui lòng nhập lý do rút tiền.", "Thiếu lý do", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _txtReason.Focus();
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }
}
