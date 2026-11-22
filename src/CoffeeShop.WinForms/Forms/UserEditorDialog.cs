using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class UserEditorDialog : Form
{
    private readonly IUserScheduleService _service;
    private readonly int? _userId;
    private readonly TextBox _txtFullName = new();
    private readonly TextBox _txtUsername = new();
    private readonly TextBox _txtPassword = new();
    private readonly ComboBox _cboRole = new();
    private readonly CheckBox _chkActive = new();
    private readonly Button _btnSave;

    public UserEditorDialog(IUserScheduleService service, UserDto? existing)
    {
        _service = service;
        _userId = existing?.Id;

        Text = existing is null ? "Thêm nhân viên mới" : $"Chỉnh sửa tài khoản: {existing.Username}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(420, 355);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        _txtFullName.Text = existing?.FullName ?? string.Empty;
        AddField("Họ và tên *", _txtFullName, 16);

        _txtUsername.Text = existing?.Username ?? string.Empty;
        _txtUsername.ReadOnly = existing is not null;
        AddField("Tên đăng nhập *", _txtUsername, 76);

        _txtPassword.UseSystemPasswordChar = true;
        AddField(existing is null ? "Mật khẩu *" : "Mật khẩu mới (Để trống nếu giữ nguyên)", _txtPassword, 136);

        _cboRole.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboRole.DisplayMember = "Label";
        _cboRole.ValueMember = "Value";
        _cboRole.DataSource = Enum.GetValues<UserRole>()
            .Select(r => new { Value = r, Label = FormatHelper.RoleLabel(r) })
            .ToList();
        _cboRole.SelectedValue = existing?.Role ?? UserRole.Staff;
        AddField("Vai trò *", _cboRole, 196);

        _chkActive.Text = "Tài khoản đang hoạt động (Cho phép đăng nhập)";
        _chkActive.Location = new Point(20, 260);
        _chkActive.Size = new Size(380, 24);
        _chkActive.Checked = existing?.IsActive ?? true;

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Hủy", 100, 36);
        btnCancel.Location = new Point(188, 302);
        btnCancel.Click += (_, _) => { DialogResult = DialogResult.Cancel; Close(); };

        _btnSave = CoffeeTheme.CreateSuccessButton("Lưu tài khoản", 120, 36);
        _btnSave.Location = new Point(296, 302);
        _btnSave.Click += async (_, _) => await SaveAsync();

        Controls.AddRange(new Control[] { _chkActive, btnCancel, _btnSave });
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
        if (_cboRole.SelectedValue is not UserRole role)
            return;

        _btnSave.Enabled = false;
        var res = await _service.SaveUserAsync(new SaveUserRequest(
            _userId,
            _txtFullName.Text,
            _txtUsername.Text,
            _txtPassword.Text,
            role,
            _chkActive.Checked));

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
