using CoffeeShop.Application.Common;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class LoginForm : Form
{
    private readonly IAuthService _authService;
    private readonly AppSession _appSession;
    private readonly AppSettings _settings;

    private readonly TextBox _txtUsername = new();
    private readonly TextBox _txtPassword = new();
    private readonly Label _lblError = new();
    private readonly Button _btnLogin;

    public LoginForm(IAuthService authService, AppSession appSession, AppSettings settings)
    {
        _authService = authService;
        _appSession = appSession;
        _settings = settings;

        Text = $"Đăng nhập | {_settings.StoreName}";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedSingle;
        MaximizeBox = false;
        ClientSize = new Size(440, 480);
        BackColor = CoffeeTheme.Cream;
        Font = CoffeeTheme.BodyFont;

        var card = new Panel
        {
            BackColor = CoffeeTheme.Surface,
            Size = new Size(376, 416),
            Location = new Point(32, 32),
            BorderStyle = BorderStyle.FixedSingle,
            Padding = new Padding(24)
        };

        var picLogo = new PictureBox
        {
            Size = new Size(64, 64),
            Location = new Point(156, 18),
            SizeMode = PictureBoxSizeMode.Zoom,
            Image = ImageHelper.LoadLogo(64)
        };

        var lblTitle = new Label
        {
            Text = _settings.StoreName.ToUpperInvariant(),
            Font = CoffeeTheme.TitleFont,
            ForeColor = CoffeeTheme.Espresso,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 88),
            Size = new Size(336, 32)
        };

        var lblSubtitle = new Label
        {
            Text = "Hệ thống Quản lý Quán Cà Phê POS & Kho",
            Font = CoffeeTheme.SmallFont,
            ForeColor = CoffeeTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(20, 120),
            Size = new Size(336, 20)
        };

        var lblUser = new Label
        {
            Text = "Tên đăng nhập",
            Font = CoffeeTheme.BodyBoldFont,
            ForeColor = CoffeeTheme.TextPrimary,
            Location = new Point(28, 152),
            Size = new Size(320, 20)
        };

        _txtUsername.Location = new Point(28, 175);
        _txtUsername.Size = new Size(320, 30);
        _txtUsername.Font = new Font("Segoe UI", 10.5F);

        var lblPass = new Label
        {
            Text = "Mật khẩu",
            Font = CoffeeTheme.BodyBoldFont,
            ForeColor = CoffeeTheme.TextPrimary,
            Location = new Point(28, 215),
            Size = new Size(320, 20)
        };

        _txtPassword.Location = new Point(28, 238);
        _txtPassword.Size = new Size(320, 30);
        _txtPassword.Font = new Font("Segoe UI", 10.5F);
        _txtPassword.UseSystemPasswordChar = true;

        _lblError.Location = new Point(28, 274);
        _lblError.Size = new Size(320, 38);
        _lblError.ForeColor = CoffeeTheme.DangerRed;
        _lblError.Font = CoffeeTheme.SmallFont;
        _lblError.TextAlign = ContentAlignment.MiddleCenter;

        _btnLogin = CoffeeTheme.CreatePrimaryButton("ĐĂNG NHẬP", 320, 42);
        _btnLogin.Location = new Point(28, 318);
        _btnLogin.Click += async (_, _) => await AttemptLoginAsync();

        var lblHint = new Label
        {
            Text = "Tài khoản mặc định: admin / staff / thukho  |  MK: 123456",
            Font = new Font("Segoe UI", 8F),
            ForeColor = CoffeeTheme.TextMuted,
            TextAlign = ContentAlignment.MiddleCenter,
            Location = new Point(16, 374),
            Size = new Size(344, 24)
        };

        card.Controls.AddRange(new Control[]
        {
            picLogo, lblTitle, lblSubtitle,
            lblUser, _txtUsername,
            lblPass, _txtPassword,
            _lblError, _btnLogin, lblHint
        });

        Controls.Add(card);
        AcceptButton = _btnLogin;
    }

    private async Task AttemptLoginAsync()
    {
        _lblError.Text = string.Empty;
        _btnLogin.Enabled = false;
        _btnLogin.Text = "Đang kiểm tra...";

        try
        {
            var result = await _authService.LoginAsync(_txtUsername.Text, _txtPassword.Text);
            if (!result.Success || result.User is null)
            {
                _lblError.Text = result.Message;
                _txtPassword.SelectAll();
                _txtPassword.Focus();
                return;
            }

            _appSession.SetUser(result.User);
            DialogResult = DialogResult.OK;
            Close();
        }
        catch (Exception ex)
        {
            _lblError.Text = $"Lỗi kết nối dữ liệu: {ex.Message}";
        }
        finally
        {
            if (!IsDisposed)
            {
                _btnLogin.Enabled = true;
                _btnLogin.Text = "ĐĂNG NHẬP";
            }
        }
    }
}
