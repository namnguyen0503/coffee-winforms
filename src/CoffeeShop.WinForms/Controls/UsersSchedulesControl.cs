using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Forms;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Controls;

public class UsersSchedulesControl : UserControl
{
    private readonly IUserScheduleService _service;

    private readonly TextBox _txtSearchUser = new();
    private readonly DataGridView _userGrid = new();

    private readonly DateTimePicker _dtStart = new();
    private readonly DateTimePicker _dtEnd = new();
    private readonly ComboBox _cboAssignUser = new();
    private readonly DateTimePicker _dtAssignDate = new();
    private readonly ComboBox _cboAssignShift = new();
    private readonly DataGridView _scheduleGrid = new();

    private List<UserDto> _users = new();
    private List<WorkScheduleDto> _schedules = new();

    public UsersSchedulesControl(IUserScheduleService service)
    {
        _service = service;
        Dock = DockStyle.Fill;
        BackColor = CoffeeTheme.Cream;
        Padding = new Padding(16);

        var tabs = new TabControl { Dock = DockStyle.Fill, Font = CoffeeTheme.BodyBoldFont };
        var tabUsers = new TabPage("Tài khoản nhân sự") { BackColor = CoffeeTheme.Cream, Padding = new Padding(8) };
        var tabSchedules = new TabPage("Phân ca làm việc") { BackColor = CoffeeTheme.Cream, Padding = new Padding(8) };

        BuildUsersTab(tabUsers);
        BuildSchedulesTab(tabSchedules);

        tabs.TabPages.Add(tabUsers);
        tabs.TabPages.Add(tabSchedules);
        Controls.Add(tabs);
    }

    private void BuildUsersTab(TabPage tab)
    {
        var bar = CoffeeTheme.CreateCardPanel(new Padding(10));
        bar.Dock = DockStyle.Top;
        bar.Height = 58;

        var flow = new FlowLayoutPanel { Dock = DockStyle.Fill };
        _txtSearchUser.Width = 220;
        _txtSearchUser.PlaceholderText = "Tìm họ tên hoặc username...";
        _txtSearchUser.TextChanged += async (_, _) => await LoadUsersAsync();

        var btnAdd = CoffeeTheme.CreateSuccessButton("+ Thêm nhân viên", 145, 32);
        btnAdd.Click += async (_, _) => await OpenUserEditorAsync(null);

        var btnEdit = CoffeeTheme.CreatePrimaryButton("Sửa / Đổi mật khẩu", 150, 32);
        btnEdit.Click += async (_, _) =>
        {
            var u = GetSelectedUser();
            if (u is not null) await OpenUserEditorAsync(u);
        };

        flow.Controls.AddRange(new Control[] { _txtSearchUser, btnAdd, btnEdit });
        bar.Controls.Add(flow);

        var gridCard = CoffeeTheme.CreateCardPanel();
        gridCard.Dock = DockStyle.Fill;
        CoffeeTheme.StyleDataGridView(_userGrid);
        _userGrid.Dock = DockStyle.Fill;
        _userGrid.ReadOnly = true;
        _userGrid.CellDoubleClick += async (_, _) =>
        {
            var u = GetSelectedUser();
            if (u is not null) await OpenUserEditorAsync(u);
        };

        _userGrid.Columns.Add("Id", "ID");
        _userGrid.Columns.Add("FullName", "Họ và tên");
        _userGrid.Columns.Add("Username", "Tên đăng nhập");
        _userGrid.Columns.Add("Role", "Vai trò");
        _userGrid.Columns.Add("Status", "Trạng thái");
        _userGrid.Columns["Id"]!.FillWeight = 35;

        gridCard.Controls.Add(_userGrid);
        tab.Controls.Add(gridCard);
        tab.Controls.Add(bar);
    }

    private void BuildSchedulesTab(TabPage tab)
    {
        var topStack = new Panel { Dock = DockStyle.Top, Height = 122 };

        var filterBar = CoffeeTheme.CreateCardPanel(new Padding(8));
        filterBar.Dock = DockStyle.Top;
        filterBar.Height = 56;
        var fFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };
        _dtStart.Format = DateTimePickerFormat.Short;
        _dtStart.Width = 115;
        _dtStart.Value = DateTime.Today;
        _dtEnd.Format = DateTimePickerFormat.Short;
        _dtEnd.Width = 115;
        _dtEnd.Value = DateTime.Today.AddDays(6);

        var btnLoad = CoffeeTheme.CreatePrimaryButton("Xem lịch", 100, 32);
        btnLoad.Click += async (_, _) => await LoadSchedulesAsync();

        var btnDelete = CoffeeTheme.CreateDangerButton("Xóa phân ca", 115, 32);
        btnDelete.Click += async (_, _) => await DeleteSelectedScheduleAsync();

        fFlow.Controls.AddRange(new Control[]
        {
            new Label { Text = "Từ ngày:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) },
            _dtStart,
            new Label { Text = "Đến ngày:", AutoSize = true, Padding = new Padding(8, 8, 4, 0) },
            _dtEnd,
            btnLoad,
            btnDelete
        });
        filterBar.Controls.Add(fFlow);

        var addBar = CoffeeTheme.CreateCardPanel(new Padding(8));
        addBar.Dock = DockStyle.Bottom;
        addBar.Height = 58;
        var aFlow = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = false };

        _cboAssignUser.Width = 190;
        _cboAssignUser.DropDownStyle = ComboBoxStyle.DropDownList;

        _dtAssignDate.Format = DateTimePickerFormat.Short;
        _dtAssignDate.Width = 115;
        _dtAssignDate.Value = DateTime.Today;

        _cboAssignShift.Width = 185;
        _cboAssignShift.DropDownStyle = ComboBoxStyle.DropDownList;
        _cboAssignShift.DisplayMember = "Label";
        _cboAssignShift.ValueMember = "Value";
        _cboAssignShift.DataSource = Enum.GetValues<ShiftType>()
            .Select(s => new { Value = s, Label = FormatHelper.ShiftLabel(s) })
            .ToList();

        var btnAssign = CoffeeTheme.CreateSuccessButton("+ Phân ca", 110, 32);
        btnAssign.Click += async (_, _) => await AssignScheduleAsync();

        aFlow.Controls.AddRange(new Control[]
        {
            new Label { Text = "Nhân viên:", AutoSize = true, Padding = new Padding(0, 8, 4, 0) },
            _cboAssignUser,
            new Label { Text = "Ngày:", AutoSize = true, Padding = new Padding(8, 8, 4, 0) },
            _dtAssignDate,
            _cboAssignShift,
            btnAssign
        });
        addBar.Controls.Add(aFlow);

        topStack.Controls.Add(addBar);
        topStack.Controls.Add(filterBar);

        var gridCard = CoffeeTheme.CreateCardPanel();
        gridCard.Dock = DockStyle.Fill;
        CoffeeTheme.StyleDataGridView(_scheduleGrid);
        _scheduleGrid.Dock = DockStyle.Fill;
        _scheduleGrid.ReadOnly = true;

        _scheduleGrid.Columns.Add("Date", "Ngày làm");
        _scheduleGrid.Columns.Add("DayName", "Thứ");
        _scheduleGrid.Columns.Add("Shift", "Ca làm việc");
        _scheduleGrid.Columns.Add("Staff", "Nhân viên");
        _scheduleGrid.Columns.Add("Role", "Chức vụ");

        gridCard.Controls.Add(_scheduleGrid);
        tab.Controls.Add(gridCard);
        tab.Controls.Add(topStack);
    }

    public async Task LoadAsync()
    {
        if (IsDisposed) return;
        await LoadUsersAsync();
        if (IsDisposed) return;
        await LoadSchedulesAsync();
    }

    private async Task LoadUsersAsync()
    {
        if (IsDisposed) return;
        _users = (await _service.GetUsersAsync(_txtSearchUser.Text)).ToList();
        if (IsDisposed) return;

        _userGrid.Rows.Clear();
        foreach (var u in _users)
        {
            var idx = _userGrid.Rows.Add(
                u.Id,
                u.FullName,
                u.Username,
                FormatHelper.RoleLabel(u.Role),
                u.IsActive ? "Hoạt động" : "Đã khóa");
            if (!u.IsActive)
                _userGrid.Rows[idx].DefaultCellStyle.ForeColor = CoffeeTheme.DangerRed;
        }

        var allUsers = (await _service.GetUsersAsync()).ToList();
        if (IsDisposed) return;

        _cboAssignUser.DisplayMember = "Display";
        _cboAssignUser.ValueMember = "Id";
        _cboAssignUser.DataSource = allUsers.Select(u => new { u.Id, Display = $"{u.FullName} (@{u.Username})" }).ToList();
    }

    private async Task LoadSchedulesAsync()
    {
        if (IsDisposed) return;
        _schedules = (await _service.GetSchedulesAsync(
            DateOnly.FromDateTime(_dtStart.Value),
            DateOnly.FromDateTime(_dtEnd.Value))).ToList();
        if (IsDisposed) return;

        _scheduleGrid.Rows.Clear();
        foreach (var s in _schedules)
        {
            _scheduleGrid.Rows.Add(
                s.ShiftDate.ToString("dd/MM/yyyy"),
                s.ShiftDate.DayOfWeek.ToString(),
                FormatHelper.ShiftLabel(s.ShiftType),
                s.FullName,
                FormatHelper.RoleLabel(s.Role));
        }
    }

    private UserDto? GetSelectedUser()
    {
        if (_userGrid.CurrentRow is null) return null;
        var idx = _userGrid.CurrentRow.Index;
        return idx >= 0 && idx < _users.Count ? _users[idx] : null;
    }

    private async Task OpenUserEditorAsync(UserDto? existing)
    {
        using var dlg = new UserEditorDialog(_service, existing);
        if (dlg.ShowDialog(FindForm()) == DialogResult.OK)
            await LoadUsersAsync();
    }

    private async Task AssignScheduleAsync()
    {
        if (_cboAssignUser.SelectedValue is not int userId || _cboAssignShift.SelectedValue is not ShiftType shift)
            return;

        var date = DateOnly.FromDateTime(_dtAssignDate.Value);
        var res = await _service.AddScheduleAsync(new AddScheduleRequest(userId, date, shift, false));

        if (res.RequiresUserUnlock)
        {
            if (MessageBox.Show(res.Message, "Mở khóa nhân viên?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                res = await _service.AddScheduleAsync(new AddScheduleRequest(userId, date, shift, true));
            else
                return;
        }

        MessageBox.Show(res.Message, res.Success ? "Thành công" : "Thông báo", MessageBoxButtons.OK,
            res.Success ? MessageBoxIcon.Information : MessageBoxIcon.Warning);

        if (res.Success)
            await LoadAsync();
    }

    private async Task DeleteSelectedScheduleAsync()
    {
        if (_scheduleGrid.CurrentRow is null) return;
        var idx = _scheduleGrid.CurrentRow.Index;
        if (idx < 0 || idx >= _schedules.Count) return;

        var res = await _service.DeleteScheduleAsync(_schedules[idx].Id);
        if (res.Success)
            await LoadSchedulesAsync();
    }
}
