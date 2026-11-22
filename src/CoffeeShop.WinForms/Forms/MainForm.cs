using CoffeeShop.Application.Common;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Controls;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;
using Microsoft.Extensions.DependencyInjection;

namespace CoffeeShop.WinForms.Forms;

public class MainForm : Form
{
    private readonly IServiceProvider _services;
    private readonly AppSession _appSession;
    private readonly AppSettings _settings;
    private readonly IShiftService _shiftService;

    private readonly Panel _sidebar = new();
    private readonly Panel _contentHost = new();
    private readonly Label _lblPageTitle = new();
    private readonly Label _lblUserBadge = new();
    private readonly Label _lblShiftBadge = new();
    private readonly Button _btnShiftAction;

    private readonly Dictionary<string, Button> _navButtons = new();
    private readonly Dictionary<string, UserControl> _controls = new();
    private string? _activeKey;
    private int _navVersion;
    private CustomerDisplayForm? _customerDisplay;

    public bool LoggedOut { get; private set; }

    public MainForm(
        IServiceProvider services,
        AppSession appSession,
        AppSettings settings,
        IShiftService shiftService)
    {
        _services = services;
        _appSession = appSession;
        _settings = settings;
        _shiftService = shiftService;

        var user = _appSession.CurrentUser ?? throw new InvalidOperationException("Chưa đăng nhập.");

        Text = $"{_settings.StoreName} - Quản lý Quán Cà Phê";
        WindowState = FormWindowState.Maximized;
        MinimumSize = new Size(1200, 720);
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = CoffeeTheme.Cream;
        Font = CoffeeTheme.BodyFont;
        CoffeeTheme.EnableDoubleBuffering(this);
        CoffeeTheme.EnableDoubleBuffering(_contentHost);

        // Sidebar
        _sidebar.Dock = DockStyle.Left;
        _sidebar.Width = 230;
        _sidebar.BackColor = CoffeeTheme.Espresso;
        _sidebar.Padding = new Padding(10, 12, 10, 12);

        var brandPanel = new Panel { Dock = DockStyle.Top, Height = 82 };
        var lblBrand = new Label
        {
            Text = _settings.StoreName,
            Font = new Font("Segoe UI", 13F, FontStyle.Bold),
            ForeColor = Color.White,
            Dock = DockStyle.Top,
            Height = 32,
            TextAlign = ContentAlignment.MiddleCenter
        };
        var lblSub = new Label
        {
            Text = FormatHelper.RoleLabel(user.Role),
            Font = CoffeeTheme.SmallFont,
            ForeColor = CoffeeTheme.Latte,
            Dock = DockStyle.Top,
            Height = 22,
            TextAlign = ContentAlignment.MiddleCenter
        };
        brandPanel.Controls.Add(lblSub);
        brandPanel.Controls.Add(lblBrand);

        var navPanel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            AutoScroll = true
        };

        var btnLogout = CoffeeTheme.CreateDangerButton("Đăng xuất", 206, 40);
        btnLogout.Dock = DockStyle.Bottom;
        btnLogout.Click += async (_, _) =>
        {
            try
            {
                await LogoutAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show($"Lỗi khi đăng xuất: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        _sidebar.Controls.Add(navPanel);
        _sidebar.Controls.Add(btnLogout);
        _sidebar.Controls.Add(brandPanel);

        // Top Bar
        var topBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 58,
            BackColor = CoffeeTheme.Surface,
            Padding = new Padding(16, 8, 16, 8)
        };

        _lblPageTitle.Dock = DockStyle.Left;
        _lblPageTitle.Width = 310;
        _lblPageTitle.Font = CoffeeTheme.TitleFont;
        _lblPageTitle.ForeColor = CoffeeTheme.Espresso;
        _lblPageTitle.TextAlign = ContentAlignment.MiddleLeft;

        var rightTopFlow = new FlowLayoutPanel
        {
            Dock = DockStyle.Right,
            Width = 630,
            FlowDirection = FlowDirection.RightToLeft,
            WrapContents = false,
            Padding = new Padding(0, 2, 0, 0)
        };

        var btnCustomerDisplay = CoffeeTheme.CreateSecondaryButton("Màn hình khách", 136, 34);
        btnCustomerDisplay.Visible = user.Role != UserRole.WarehouseStaff;
        btnCustomerDisplay.Margin = new Padding(0, 0, 8, 0);
        btnCustomerDisplay.Click += (_, _) => ToggleCustomerDisplay();

        _btnShiftAction = CoffeeTheme.CreatePrimaryButton("Mở ca", 110, 34);
        _btnShiftAction.Visible = user.Role != UserRole.WarehouseStaff;
        _btnShiftAction.Click += async (_, _) =>
        {
            try
            {
                await ToggleShiftAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show($"Lỗi thao tác ca: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        _lblShiftBadge.AutoSize = true;
        _lblShiftBadge.Font = CoffeeTheme.BodyBoldFont;
        _lblShiftBadge.Padding = new Padding(10, 8, 14, 0);
        _lblShiftBadge.Visible = user.Role != UserRole.WarehouseStaff;

        _lblUserBadge.AutoSize = true;
        _lblUserBadge.Font = CoffeeTheme.BodyFont;
        _lblUserBadge.ForeColor = CoffeeTheme.TextMuted;
        _lblUserBadge.Padding = new Padding(8, 8, 12, 0);
        _lblUserBadge.Text = $"Xin chào, {user.FullName}";

        rightTopFlow.Controls.AddRange(new Control[] { _btnShiftAction, btnCustomerDisplay, _lblShiftBadge, _lblUserBadge });
        topBar.Controls.Add(rightTopFlow);
        topBar.Controls.Add(_lblPageTitle);

        _contentHost.Dock = DockStyle.Fill;

        Controls.Add(_contentHost);
        Controls.Add(topBar);
        Controls.Add(_sidebar);

        BuildMenuItems(user.Role, navPanel);

        Shown += async (_, _) =>
        {
            try
            {
                if (IsDisposed) return;
                await RefreshShiftBadgeAsync();
                if (IsDisposed) return;
                var initialKey = user.Role == UserRole.WarehouseStaff ? "warehouse" : "dashboard";
                await NavigateAsync(initialKey);

                if (!IsDisposed && user.Role is UserRole.Admin or UserRole.Staff)
                    _ = PreloadPosThumbnailsInBackgroundAsync();
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show($"Lỗi tải dữ liệu màn hình chính: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };
    }

    private void BuildMenuItems(UserRole role, FlowLayoutPanel navPanel)
    {
        if (role is UserRole.Admin or UserRole.Staff)
        {
            AddNavButton(navPanel, "dashboard", "Tổng quan");
            AddNavButton(navPanel, "pos", "Bán hàng (POS)");
            AddNavButton(navPanel, "orders", "Đơn hàng");
        }

        if (role == UserRole.Admin)
            AddNavButton(navPanel, "menu", "Thực đơn & Công thức");

        if (role is UserRole.Admin or UserRole.WarehouseStaff)
            AddNavButton(navPanel, "warehouse", "Kho nguyên liệu");

        if (role == UserRole.Admin)
        {
            AddNavButton(navPanel, "vouchers", "Khuyến mãi (Voucher)");
            AddNavButton(navPanel, "users", "Nhân sự & Lịch làm");
            AddNavButton(navPanel, "reports", "Báo cáo & Dòng tiền");
        }
    }

    private void AddNavButton(FlowLayoutPanel host, string key, string label)
    {
        var btn = new Button
        {
            Text = "  " + label,
            TextAlign = ContentAlignment.MiddleLeft,
            Size = new Size(206, 42),
            Margin = new Padding(0, 3, 0, 3),
            FlatStyle = FlatStyle.Flat,
            BackColor = CoffeeTheme.Espresso,
            ForeColor = CoffeeTheme.Latte,
            Font = CoffeeTheme.BodyBoldFont,
            Cursor = Cursors.Hand,
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        btn.FlatAppearance.MouseOverBackColor = CoffeeTheme.Mocha;
        btn.Click += async (_, _) =>
        {
            try
            {
                await NavigateAsync(key);
            }
            catch (Exception ex)
            {
                if (!IsDisposed)
                    MessageBox.Show($"Lỗi chuyển màn hình: {ex.Message}", "Lỗi", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        _navButtons[key] = btn;
        host.Controls.Add(btn);
    }

    internal async Task NavigateAsync(string key)
    {
        if (!_navButtons.ContainsKey(key) || IsDisposed)
            return;

        var navId = ++_navVersion;
        _activeKey = key;
        foreach (var (k, btn) in _navButtons)
        {
            var isCurrent = k == key;
            btn.BackColor = isCurrent ? CoffeeTheme.Mocha : CoffeeTheme.Espresso;
            btn.ForeColor = isCurrent ? Color.White : CoffeeTheme.Latte;
        }

        _lblPageTitle.Text = _navButtons[key].Text.Trim();

        var isNew = !_controls.TryGetValue(key, out var control);
        if (isNew)
        {
            control = CreateControl(key);
            control.Visible = false;
            _controls[key] = control;
            _contentHost.Controls.Add(control);

            await RefreshControlAsync(key, control);
            if (IsDisposed || navId != _navVersion)
                return;

            ShowActiveControl(control);
            return;
        }

        ShowActiveControl(control!);
        await RefreshControlAsync(key, control!);
    }

    private void ShowActiveControl(UserControl active)
    {
        CoffeeTheme.SuspendDrawing(_contentHost);
        _contentHost.SuspendLayout();
        try
        {
            foreach (var c in _controls.Values)
            {
                if (!ReferenceEquals(c, active) && c.Visible)
                    c.Visible = false;
            }
            active.Visible = true;
            active.BringToFront();
        }
        finally
        {
            _contentHost.ResumeLayout();
            CoffeeTheme.ResumeDrawing(_contentHost);
        }
    }

    private async Task PreloadPosThumbnailsInBackgroundAsync()
    {
        try
        {
            var menuService = _services.GetRequiredService<IMenuService>();
            var products = await menuService.GetProductsAsync(includeUnavailable: false);
            if (IsDisposed)
                return;

            await ImageHelper.PreloadProductImagesAsync(
                products.Select(p => (p.ImagePath, p.Name)),
                160);
        }
        catch
        {
            // Ignore background warm-up errors
        }
    }

    private UserControl CreateControl(string key) => key switch
    {
        "dashboard" => new DashboardControl(
            _services.GetRequiredService<IReportService>(),
            _shiftService,
            _appSession,
            k => _ = NavigateAsync(k)),
        "pos" => new PosControl(
            _services.GetRequiredService<IMenuService>(),
            _services.GetRequiredService<IOrderService>(),
            _services.GetRequiredService<IVoucherService>(),
            _shiftService,
            _appSession,
            _settings,
            () => _ = RefreshShiftBadgeAsync(),
            () => _customerDisplay,
            ToggleCustomerDisplay),
        "orders" => new OrdersControl(
            _services.GetRequiredService<IOrderService>(),
            _services.GetRequiredService<ICsvExportService>(),
            _appSession,
            _settings),
        "menu" => new MenuManagementControl(
            _services.GetRequiredService<IMenuService>(),
            _services.GetRequiredService<IInventoryService>()),
        "warehouse" => new WarehouseControl(
            _services.GetRequiredService<IInventoryService>(),
            _services.GetRequiredService<IUserScheduleService>(),
            _services.GetRequiredService<ICsvExportService>(),
            _appSession),
        "vouchers" => new VouchersControl(_services.GetRequiredService<IVoucherService>()),
        "users" => new UsersSchedulesControl(_services.GetRequiredService<IUserScheduleService>()),
        "reports" => new ReportsControl(
            _services.GetRequiredService<IReportService>(),
            _shiftService,
            _services.GetRequiredService<ICsvExportService>(),
            _appSession),
        _ => throw new ArgumentOutOfRangeException(nameof(key))
    };

    private static Task RefreshControlAsync(string key, UserControl control) => key switch
    {
        "dashboard" => ((DashboardControl)control).LoadAsync(),
        "pos" => ((PosControl)control).LoadAsync(),
        "orders" => ((OrdersControl)control).LoadAsync(),
        "menu" => ((MenuManagementControl)control).LoadAsync(),
        "warehouse" => ((WarehouseControl)control).LoadAsync(),
        "vouchers" => ((VouchersControl)control).LoadAsync(),
        "users" => ((UsersSchedulesControl)control).LoadAsync(),
        "reports" => ((ReportsControl)control).LoadAsync(),
        _ => Task.CompletedTask
    };

    private async Task RefreshShiftBadgeAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is null || user.Role == UserRole.WarehouseStaff || IsDisposed)
            return;

        var session = await _shiftService.GetActiveSessionAsync(user.Id);
        if (IsDisposed) return;

        _appSession.SetActiveSession(session);

        if (session is null)
        {
            _lblShiftBadge.ForeColor = CoffeeTheme.WarningOrange;
            _lblShiftBadge.Text = "Chưa vào ca";
            _btnShiftAction.Text = "Mở ca";
            _btnShiftAction.BackColor = CoffeeTheme.AccentGreen;
        }
        else
        {
            _lblShiftBadge.ForeColor = CoffeeTheme.AccentGreen;
            _lblShiftBadge.Text = $"Ca #{session.Id} đang mở ({session.StartTime:HH:mm})";
            _btnShiftAction.Text = "Chốt ca";
            _btnShiftAction.BackColor = CoffeeTheme.DangerRed;
        }
    }

    private async Task ToggleShiftAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is null || IsDisposed) return;

        if (_appSession.ActiveSession is null)
        {
            var anyOpen = await _shiftService.GetAnyActiveSessionAsync();
            if (IsDisposed) return;

            using var dlg = new StartShiftDialog(_shiftService, user.Id, anyOpen);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                await RefreshShiftBadgeAsync();
        }
        else
        {
            var preview = await _shiftService.GetCurrentShiftPreviewAsync(user.Id);
            if (IsDisposed) return;

            if (!preview.Success || preview.Data is null)
            {
                MessageBox.Show(preview.Message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            using var dlg = new EndShiftDialog(_shiftService, user.Id, preview.Data);
            if (dlg.ShowDialog(this) == DialogResult.OK)
                await RefreshShiftBadgeAsync();
        }

        if (IsDisposed) return;
        if (_activeKey == "dashboard" && _controls.TryGetValue("dashboard", out var dash))
            await ((DashboardControl)dash).LoadAsync();
    }

    private async Task LogoutAsync()
    {
        var user = _appSession.CurrentUser;
        if (user is not null && user.Role != UserRole.WarehouseStaff && !IsDisposed)
        {
            var active = await _shiftService.GetActiveSessionAsync(user.Id);
            if (IsDisposed) return;

            if (active is not null)
            {
                var confirm = MessageBox.Show(
                    "Bạn đang có ca làm việc đang mở. Chốt ca trước khi đăng xuất?",
                    "Xác nhận chốt ca",
                    MessageBoxButtons.YesNoCancel,
                    MessageBoxIcon.Question);

                if (confirm == DialogResult.Cancel)
                    return;

                if (confirm == DialogResult.Yes)
                {
                    var preview = await _shiftService.GetCurrentShiftPreviewAsync(user.Id);
                    if (IsDisposed) return;

                    if (preview.Success && preview.Data is not null)
                    {
                        using var dlg = new EndShiftDialog(_shiftService, user.Id, preview.Data);
                        if (dlg.ShowDialog(this) != DialogResult.OK)
                            return;
                    }
                }
            }
        }

        if (IsDisposed) return;
        _appSession.Clear();
        LoggedOut = true;
        Close();
    }

    private void ToggleCustomerDisplay()
    {
        if (_customerDisplay is { IsDisposed: false })
        {
            _customerDisplay.Focus();
        }
        else
        {
            _customerDisplay = new CustomerDisplayForm(_settings);
            _customerDisplay.PositionOnSecondaryScreenIfAvailable();
            _customerDisplay.Show();
        }

        if (_controls.TryGetValue("pos", out var ctrl) && ctrl is PosControl pos)
            pos.SyncCustomerDisplay();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _customerDisplay is { IsDisposed: false })
        {
            _customerDisplay.Close();
            _customerDisplay.Dispose();
            _customerDisplay = null;
        }
        base.Dispose(disposing);
    }
}
