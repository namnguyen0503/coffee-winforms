using CoffeeShop.Application.Common;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public record CustomerDisplayItem(string Name, int Quantity, decimal UnitPrice, string? Note, string? ImagePath = null)
{
    public decimal LineTotal => UnitPrice * Quantity;
}

public class CustomerDisplayForm : Form
{
    private readonly AppSettings _settings;
    private readonly PictureBox _picLogo = new();
    private readonly PictureBox _picQr = new();
    private readonly Label _lblStateTitle = new();
    private readonly Label _lblStateDetail = new();
    private readonly FlowLayoutPanel _cartBody = new();
    private readonly Label _lblSubtotal = new();
    private readonly Label _lblDiscount = new();
    private readonly Label _lblTotal = new();
    private readonly System.Windows.Forms.Timer _successResetTimer = new() { Interval = 5000 };

    private CancellationTokenSource? _qrCts;
    private IReadOnlyList<CustomerDisplayItem> _currentItems = Array.Empty<CustomerDisplayItem>();
    private bool _isFullscreen;

    public CustomerDisplayForm(AppSettings settings)
    {
        _settings = settings;

        Text = $"Màn hình Khách hàng | {_settings.StoreName}";
        Size = new Size(960, 620);
        MinimumSize = new Size(760, 480);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = CoffeeTheme.Cream;
        Font = CoffeeTheme.BodyFont;
        KeyPreview = true;
        CoffeeTheme.EnableDoubleBuffering(this);

        KeyDown += (_, e) =>
        {
            if (e.KeyCode == Keys.F11)
            {
                ToggleFullscreen(!_isFullscreen);
                e.Handled = true;
            }
            else if (e.KeyCode == Keys.Escape && _isFullscreen)
            {
                ToggleFullscreen(false);
                e.Handled = true;
            }
        };

        _successResetTimer.Tick += (_, _) =>
        {
            _successResetTimer.Stop();
            if (!IsDisposed)
            {
                ShowWelcome();
                UpdateCart(Array.Empty<CustomerDisplayItem>(), 0m, 0m, 0m);
            }
        };

        var split = CoffeeTheme.CreateSafeSplitContainer(0.55, 280, 280);

        // Left welcome / VietQR / success panel
        var leftPanel = new Panel { Dock = DockStyle.Fill, BackColor = CoffeeTheme.Surface, Padding = new Padding(24) };
        CoffeeTheme.EnableDoubleBuffering(leftPanel);

        _picLogo.Size = new Size(120, 120);
        _picLogo.SizeMode = PictureBoxSizeMode.Zoom;
        _picLogo.Image = ImageHelper.LoadLogo(120);
        _picLogo.Dock = DockStyle.Top;

        _lblStateTitle.Dock = DockStyle.Top;
        _lblStateTitle.Height = 56;
        _lblStateTitle.Font = new Font("Segoe UI", 20F, FontStyle.Bold);
        _lblStateTitle.ForeColor = CoffeeTheme.Espresso;
        _lblStateTitle.TextAlign = ContentAlignment.MiddleCenter;

        _picQr.Dock = DockStyle.Top;
        _picQr.Height = 250;
        _picQr.SizeMode = PictureBoxSizeMode.Zoom;
        _picQr.Visible = false;
        _picQr.Padding = new Padding(8);

        _lblStateDetail.Dock = DockStyle.Fill;
        _lblStateDetail.Font = new Font("Segoe UI", 12F);
        _lblStateDetail.ForeColor = CoffeeTheme.TextMuted;
        _lblStateDetail.TextAlign = ContentAlignment.TopCenter;

        leftPanel.Controls.Add(_lblStateDetail);
        leftPanel.Controls.Add(_picQr);
        leftPanel.Controls.Add(_lblStateTitle);
        leftPanel.Controls.Add(_picLogo);
        split.Panel1.Controls.Add(leftPanel);

        // Right cart panel
        var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = CoffeeTheme.Surface };
        CoffeeTheme.EnableDoubleBuffering(rightPanel);

        var header = new Label
        {
            Text = "  THÔNG TIN ĐƠN HÀNG (F11: Toàn màn hình)",
            Dock = DockStyle.Top,
            Height = 46,
            BackColor = CoffeeTheme.Espresso,
            ForeColor = Color.White,
            Font = CoffeeTheme.SectionFont,
            TextAlign = ContentAlignment.MiddleLeft
        };

        _cartBody.Dock = DockStyle.Fill;
        _cartBody.FlowDirection = FlowDirection.TopDown;
        _cartBody.WrapContents = false;
        _cartBody.AutoScroll = true;
        _cartBody.BackColor = CoffeeTheme.Surface;
        _cartBody.Padding = new Padding(8);
        CoffeeTheme.EnableDoubleBuffering(_cartBody);
        _cartBody.SizeChanged += (_, _) => ResizeCartRowWidths();

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 120,
            BackColor = CoffeeTheme.Cream,
            Padding = new Padding(14)
        };

        _lblSubtotal.Dock = DockStyle.Top;
        _lblSubtotal.Height = 24;
        _lblSubtotal.Font = CoffeeTheme.BodyFont;

        _lblDiscount.Dock = DockStyle.Top;
        _lblDiscount.Height = 24;
        _lblDiscount.ForeColor = CoffeeTheme.AccentGreen;
        _lblDiscount.Font = CoffeeTheme.BodyBoldFont;

        _lblTotal.Dock = DockStyle.Fill;
        _lblTotal.Font = new Font("Segoe UI", 16F, FontStyle.Bold);
        _lblTotal.ForeColor = CoffeeTheme.DangerRed;
        _lblTotal.TextAlign = ContentAlignment.MiddleLeft;

        footer.Controls.Add(_lblTotal);
        footer.Controls.Add(_lblDiscount);
        footer.Controls.Add(_lblSubtotal);

        rightPanel.Controls.Add(_cartBody);
        rightPanel.Controls.Add(footer);
        rightPanel.Controls.Add(header);
        split.Panel2.Controls.Add(rightPanel);

        Controls.Add(split);
        ShowWelcome();
        UpdateCart(Array.Empty<CustomerDisplayItem>(), 0m, 0m, 0m);
    }

    public void PositionOnSecondaryScreenIfAvailable()
    {
        // Preserve explicit Manual location (e.g., off-screen automated UI tests)
        if (StartPosition == FormStartPosition.Manual)
            return;

        var secondary = Screen.AllScreens.FirstOrDefault(s => !s.Primary);
        if (secondary is null)
            return;

        StartPosition = FormStartPosition.Manual;
        Bounds = secondary.Bounds;
        WindowState = FormWindowState.Maximized;
        _isFullscreen = true;
    }

    public void ToggleFullscreen(bool enable)
    {
        _isFullscreen = enable;
        if (enable)
        {
            FormBorderStyle = FormBorderStyle.None;
            WindowState = FormWindowState.Maximized;
        }
        else
        {
            FormBorderStyle = FormBorderStyle.Sizable;
            WindowState = FormWindowState.Normal;
        }
    }

    public void ShowWelcome()
    {
        _successResetTimer.Stop();
        CancelQrLoad();
        ClearQrImage();
        _picQr.Visible = false;
        _picLogo.Visible = true;

        _lblStateTitle.ForeColor = CoffeeTheme.Espresso;
        _lblStateTitle.Text = _settings.StoreName;
        _lblStateDetail.Text = $"\nXin chào quý khách!\nMời quý khách gọi món tại quầy.\n\n{_settings.WifiInfo}";
    }

    public void ShowTransferPayment(decimal amount, string? memo = null)
    {
        _successResetTimer.Stop();
        CancelQrLoad();
        ClearQrImage();

        _picLogo.Visible = false;
        _picQr.Visible = true;
        _lblStateTitle.ForeColor = CoffeeTheme.AccentBlue;
        _lblStateTitle.Text = "QUÉT MÃ THANH TOÁN";
        var effectiveMemo = string.IsNullOrWhiteSpace(memo)
            ? $"POS {DateTime.Now:HH}h{DateTime.Now:mm}"
            : memo.Trim();

        _lblStateDetail.Text =
            $"Đang tải mã VietQR...\n" +
            $"Ngân hàng: {_settings.BankCode}  |  STK: {_settings.BankAccountNo}\n" +
            $"Chủ TK: {_settings.BankAccountName}\n" +
            $"SỐ TIỀN: {FormatHelper.Currency(amount)}";

        var cts = new CancellationTokenSource();
        _qrCts = cts;
        _ = LoadCustomerQrAsync(amount, effectiveMemo, cts.Token);
    }

    private async Task LoadCustomerQrAsync(decimal amount, string memo, CancellationToken ct)
    {
        var qrImg = await ImageHelper.LoadVietQrAsync(_settings, amount, memo, ct);
        if (ct.IsCancellationRequested || IsDisposed)
        {
            qrImg?.Dispose();
            return;
        }

        void ApplyUi()
        {
            if (ct.IsCancellationRequested || IsDisposed)
            {
                qrImg?.Dispose();
                return;
            }

            ClearQrImage();
            if (qrImg is not null)
            {
                _picQr.Image = qrImg;
                _picQr.Visible = true;
                _lblStateDetail.Text =
                    $"Ngân hàng: {_settings.BankCode}  |  STK: {_settings.BankAccountNo}\n" +
                    $"Chủ TK: {_settings.BankAccountName}\n" +
                    $"SỐ TIỀN: {FormatHelper.Currency(amount)}";
            }
            else
            {
                _picQr.Visible = false;
                _lblStateDetail.Text =
                    $"\nNgân hàng: {_settings.BankCode}\n" +
                    $"Số tài khoản: {_settings.BankAccountNo}\n" +
                    $"Chủ tài khoản: {_settings.BankAccountName}\n\n" +
                    $"SỐ TIỀN: {FormatHelper.Currency(amount)}";
            }
        }

        if (InvokeRequired)
            BeginInvoke((Action)ApplyUi);
        else
            ApplyUi();
    }

    public void ShowPaymentSuccess()
    {
        CancelQrLoad();
        ClearQrImage();
        _picQr.Visible = false;
        _picLogo.Visible = true;

        _lblStateTitle.ForeColor = CoffeeTheme.AccentGreen;
        _lblStateTitle.Text = "THANH TOÁN THÀNH CÔNG!";
        _lblStateDetail.Text = "\nCảm ơn quý khách và hẹn gặp lại!";

        _successResetTimer.Stop();
        _successResetTimer.Start();
    }

    public void UpdateCart(IReadOnlyList<CustomerDisplayItem> items, decimal subtotal, decimal discount, decimal finalAmount)
    {
        _currentItems = items;
        RenderCustomerCart(items);

        _lblSubtotal.Text = $"Tạm tính: {FormatHelper.Currency(subtotal)}";
        _lblDiscount.Text = $"Giảm giá: -{FormatHelper.Currency(discount)}";
        _lblTotal.Text = $"TỔNG CỘNG: {FormatHelper.Currency(finalAmount)}";
    }

    private void RenderCustomerCart(IReadOnlyList<CustomerDisplayItem> items)
    {
        var rowWidth = Math.Max(220, _cartBody.ClientSize.Width - 22);
        var built = new List<Control>(Math.Max(1, items.Count));

        if (items.Count == 0)
        {
            built.Add(new Label
            {
                Text = "\n\nChưa có món nào được chọn.\nMời quý khách gọi món.",
                Font = CoffeeTheme.BodyFont,
                ForeColor = CoffeeTheme.TextMuted,
                Size = new Size(rowWidth, 120),
                TextAlign = ContentAlignment.MiddleCenter
            });
        }
        else
        {
            foreach (var item in items)
            {
                var card = new Panel
                {
                    Size = new Size(rowWidth, 68),
                    BackColor = CoffeeTheme.Surface,
                    BorderStyle = BorderStyle.FixedSingle,
                    Margin = new Padding(0, 0, 0, 6),
                    Padding = new Padding(8, 6, 10, 6)
                };

                var pic = new PictureBox
                {
                    Size = new Size(48, 48),
                    Location = new Point(8, 9),
                    SizeMode = PictureBoxSizeMode.Zoom,
                    Image = ImageHelper.LoadProductImage(item.ImagePath, item.Name, 96),
                    BackColor = CoffeeTheme.Cream
                };

                var lblTotal = new Label
                {
                    Text = FormatHelper.Currency(item.LineTotal),
                    Font = CoffeeTheme.BodyBoldFont,
                    ForeColor = CoffeeTheme.Espresso,
                    Dock = DockStyle.Right,
                    Width = 98,
                    TextAlign = ContentAlignment.MiddleRight
                };

                var textHost = new Panel
                {
                    Location = new Point(64, 6),
                    Size = new Size(Math.Max(80, rowWidth - 174), 54)
                };

                var lblName = new Label
                {
                    Text = item.Name,
                    Font = CoffeeTheme.BodyBoldFont,
                    ForeColor = CoffeeTheme.TextPrimary,
                    Dock = DockStyle.Top,
                    Height = 20,
                    AutoEllipsis = true
                };

                var metaText = string.IsNullOrWhiteSpace(item.Note)
                    ? $"{item.Quantity} x {item.UnitPrice:N0} đ"
                    : $"{item.Quantity} x {item.UnitPrice:N0} đ  •  {item.Note}";

                var lblMeta = new Label
                {
                    Text = metaText,
                    Font = CoffeeTheme.SmallFont,
                    ForeColor = string.IsNullOrWhiteSpace(item.Note) ? CoffeeTheme.TextMuted : CoffeeTheme.AccentBlue,
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true
                };

                textHost.Controls.Add(lblMeta);
                textHost.Controls.Add(lblName);

                card.Controls.Add(textHost);
                card.Controls.Add(pic);
                card.Controls.Add(lblTotal);
                built.Add(card);
            }
        }

        CoffeeTheme.SuspendDrawing(_cartBody);
        _cartBody.SuspendLayout();
        try
        {
            DisposeCartControls();
            _cartBody.Controls.AddRange(built.ToArray());
        }
        finally
        {
            _cartBody.ResumeLayout();
            CoffeeTheme.ResumeDrawing(_cartBody);
        }
    }

    private void ResizeCartRowWidths()
    {
        if (_cartBody.IsDisposed || _cartBody.Controls.Count == 0)
            return;

        var rowWidth = Math.Max(220, _cartBody.ClientSize.Width - 22);
        _cartBody.SuspendLayout();
        foreach (Control c in _cartBody.Controls)
        {
            if (c.Width != rowWidth)
                c.Width = rowWidth;
        }
        _cartBody.ResumeLayout();
    }

    private void DisposeCartControls()
    {
        foreach (Control c in _cartBody.Controls)
        {
            foreach (Control child in c.Controls)
            {
                if (child is PictureBox pb && pb.Image is not null)
                {
                    var img = pb.Image;
                    pb.Image = null;
                    img.Dispose();
                }
            }
            c.Dispose();
        }
        _cartBody.Controls.Clear();
    }

    private void CancelQrLoad()
    {
        if (_qrCts is not null)
        {
            _qrCts.Cancel();
            _qrCts.Dispose();
            _qrCts = null;
        }
    }

    private void ClearQrImage()
    {
        if (_picQr.Image is not null)
        {
            var img = _picQr.Image;
            _picQr.Image = null;
            img.Dispose();
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _successResetTimer.Stop();
            _successResetTimer.Dispose();
            CancelQrLoad();
            ClearQrImage();
            if (_picLogo.Image is not null)
            {
                var logo = _picLogo.Image;
                _picLogo.Image = null;
                logo.Dispose();
            }
            DisposeCartControls();
        }
        base.Dispose(disposing);
    }
}
