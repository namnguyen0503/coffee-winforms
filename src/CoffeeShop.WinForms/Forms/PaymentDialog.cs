using CoffeeShop.Application.Common;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class PaymentDialog : Form
{
    private readonly decimal _finalAmount;
    private readonly AppSettings _settings;

    private readonly RadioButton _rbCash = new();
    private readonly RadioButton _rbTransfer = new();
    private readonly Panel _cashPanel = new();
    private readonly Panel _transferPanel = new();
    private readonly NumericUpDown _numReceived = new();
    private readonly Label _lblChange = new();
    private readonly PictureBox _picQr = new();
    private readonly Label _lblTransferInfo = new();

    private CancellationTokenSource? _qrCts;
    private bool _qrLoaded;

    public PaymentMethod SelectedMethod { get; private set; } = PaymentMethod.Cash;

    public PaymentDialog(decimal finalAmount, AppSettings settings, Action<PaymentMethod>? onMethodChanged = null)
    {
        _finalAmount = finalAmount;
        _settings = settings;

        Text = "Thanh toán đơn hàng";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(490, 420);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        var lblTotal = new Label
        {
            Text = $"TỔNG CẦN THANH TOÁN: {FormatHelper.Currency(_finalAmount)}",
            Font = new Font("Segoe UI", 14F, FontStyle.Bold),
            ForeColor = CoffeeTheme.DangerRed,
            BackColor = CoffeeTheme.Cream,
            TextAlign = ContentAlignment.MiddleCenter,
            Dock = DockStyle.Top,
            Height = 56
        };

        var methodBox = new FlowLayoutPanel
        {
            Location = new Point(24, 70),
            Size = new Size(442, 38),
            FlowDirection = FlowDirection.LeftToRight
        };

        _rbCash.Text = "Tiền mặt";
        _rbCash.Font = CoffeeTheme.BodyBoldFont;
        _rbCash.AutoSize = true;
        _rbCash.Checked = true;
        _rbCash.Margin = new Padding(0, 6, 30, 0);

        _rbTransfer.Text = "Chuyển khoản QR";
        _rbTransfer.Font = CoffeeTheme.BodyBoldFont;
        _rbTransfer.AutoSize = true;
        _rbTransfer.Margin = new Padding(0, 6, 0, 0);

        _rbCash.CheckedChanged += (_, _) =>
        {
            if (!_rbCash.Checked) return;
            SelectedMethod = PaymentMethod.Cash;
            _cashPanel.Visible = true;
            _transferPanel.Visible = false;
            onMethodChanged?.Invoke(PaymentMethod.Cash);
        };
        _rbTransfer.CheckedChanged += (_, _) =>
        {
            if (!_rbTransfer.Checked) return;
            SelectedMethod = PaymentMethod.Transfer;
            _cashPanel.Visible = false;
            _transferPanel.Visible = true;
            onMethodChanged?.Invoke(PaymentMethod.Transfer);
            EnsureQrLoaded();
        };

        methodBox.Controls.AddRange(new Control[] { _rbCash, _rbTransfer });

        // Cash panel
        _cashPanel.Location = new Point(24, 116);
        _cashPanel.Size = new Size(442, 224);
        _cashPanel.BorderStyle = BorderStyle.FixedSingle;
        _cashPanel.Padding = new Padding(14);

        var lblRec = new Label
        {
            Text = "Tiền khách đưa (VNĐ):",
            Font = CoffeeTheme.BodyBoldFont,
            Location = new Point(14, 14),
            Size = new Size(410, 22)
        };

        _numReceived.Location = new Point(14, 40);
        _numReceived.Size = new Size(410, 34);
        _numReceived.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
        _numReceived.Maximum = 1_000_000_000m;
        _numReceived.ThousandsSeparator = true;
        _numReceived.Value = Math.Clamp(_finalAmount, _numReceived.Minimum, _numReceived.Maximum);
        _numReceived.ValueChanged += (_, _) => UpdateChange();

        var quickFlow = new FlowLayoutPanel
        {
            Location = new Point(12, 86),
            Size = new Size(416, 44)
        };
        foreach (var amt in new[] { _finalAmount, 50000m, 100000m, 200000m, 500000m }.Distinct())
        {
            var btnQ = CoffeeTheme.CreateSecondaryButton(amt == _finalAmount ? "Vừa đủ" : $"{amt / 1000:N0}k", 74, 32);
            btnQ.Margin = new Padding(2);
            btnQ.Click += (_, _) => _numReceived.Value = Math.Clamp(amt, _numReceived.Minimum, _numReceived.Maximum);
            quickFlow.Controls.Add(btnQ);
        }

        _lblChange.Location = new Point(14, 148);
        _lblChange.Size = new Size(410, 36);
        _lblChange.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        UpdateChange();

        _cashPanel.Controls.AddRange(new Control[] { lblRec, _numReceived, quickFlow, _lblChange });

        // Transfer panel with VietQR + Bank Info fallback
        _transferPanel.Location = new Point(24, 116);
        _transferPanel.Size = new Size(442, 224);
        _transferPanel.BorderStyle = BorderStyle.FixedSingle;
        _transferPanel.Visible = false;
        _transferPanel.Padding = new Padding(12);

        _picQr.Location = new Point(12, 16);
        _picQr.Size = new Size(184, 192);
        _picQr.SizeMode = PictureBoxSizeMode.Zoom;
        _picQr.BackColor = CoffeeTheme.Cream;
        _picQr.BorderStyle = BorderStyle.FixedSingle;

        _lblTransferInfo.Location = new Point(208, 18);
        _lblTransferInfo.Size = new Size(222, 190);
        _lblTransferInfo.Font = CoffeeTheme.BodyFont;
        _lblTransferInfo.TextAlign = ContentAlignment.MiddleLeft;
        _lblTransferInfo.Text = FormatTransferDetails("Đang tải mã VietQR...");

        _transferPanel.Controls.Add(_picQr);
        _transferPanel.Controls.Add(_lblTransferInfo);

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Đóng", 120, 40);
        btnCancel.Location = new Point(186, 360);
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        var btnPay = CoffeeTheme.CreateSuccessButton("Xác nhận thanh toán", 152, 40);
        btnPay.Location = new Point(314, 360);
        btnPay.Click += (_, _) =>
        {
            if (SelectedMethod == PaymentMethod.Cash && _numReceived.Value < _finalAmount)
            {
                MessageBox.Show("Số tiền khách đưa chưa đủ!", "Thiếu tiền", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }
            DialogResult = DialogResult.OK;
            Close();
        };

        Controls.AddRange(new Control[] { methodBox, _cashPanel, _transferPanel, btnCancel, btnPay, lblTotal });
        AcceptButton = btnPay;
    }

    private string FormatTransferDetails(string statusLine) =>
        $"THÔNG TIN CHUYỂN KHOẢN\n\n" +
        $"Ngân hàng: {_settings.BankCode}\n" +
        $"Số TK: {_settings.BankAccountNo}\n" +
        $"Chủ TK: {_settings.BankAccountName}\n" +
        $"Số tiền: {FormatHelper.Currency(_finalAmount)}\n\n" +
        $"{statusLine}";

    private void EnsureQrLoaded()
    {
        if (_qrLoaded)
            return;

        _qrLoaded = true;
        _qrCts?.Cancel();
        _qrCts?.Dispose();
        _qrCts = new CancellationTokenSource();
        var ct = _qrCts.Token;

        _ = LoadQrAsync(ct);
    }

    private async Task LoadQrAsync(CancellationToken ct)
    {
        var img = await ImageHelper.LoadVietQrAsync(_settings, _finalAmount, $"POS {DateTime.Now:HH}h{DateTime.Now:mm}", ct);
        if (ct.IsCancellationRequested || IsDisposed)
        {
            img?.Dispose();
            return;
        }

        void Apply()
        {
            if (ct.IsCancellationRequested || IsDisposed)
            {
                img?.Dispose();
                return;
            }

            if (_picQr.Image is not null)
            {
                var old = _picQr.Image;
                _picQr.Image = null;
                old.Dispose();
            }

            if (img is not null)
            {
                _picQr.Image = img;
                _lblTransferInfo.Text = FormatTransferDetails("Quét QR & xác nhận đã nhận tiền trước khi bấm Hoàn tất.");
            }
            else
            {
                _lblTransferInfo.Text = FormatTransferDetails("(Không tải được QR - kiểm tra chuyển khoản thủ công theo STK)");
            }
        }

        if (InvokeRequired)
            BeginInvoke((Action)Apply);
        else
            Apply();
    }

    private void UpdateChange()
    {
        var change = _numReceived.Value - _finalAmount;
        if (change >= 0)
        {
            _lblChange.ForeColor = CoffeeTheme.AccentGreen;
            _lblChange.Text = $"Tiền thối lại khách: {FormatHelper.Currency(change)}";
        }
        else
        {
            _lblChange.ForeColor = CoffeeTheme.DangerRed;
            _lblChange.Text = $"Còn thiếu: {FormatHelper.Currency(Math.Abs(change))}";
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _qrCts?.Cancel();
            _qrCts?.Dispose();
            _qrCts = null;
            if (_picQr.Image is not null)
            {
                var img = _picQr.Image;
                _picQr.Image = null;
                img.Dispose();
            }
        }
        base.Dispose(disposing);
    }
}
