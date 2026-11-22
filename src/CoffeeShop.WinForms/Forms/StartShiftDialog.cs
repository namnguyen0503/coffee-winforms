using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class StartShiftDialog : Form
{
    private readonly IShiftService _shiftService;
    private readonly int _userId;
    private readonly NumericUpDown _numStartCash = new();
    private readonly Button _btnConfirm;

    public WorkSessionDto? StartedSession { get; private set; }

    public StartShiftDialog(IShiftService shiftService, int userId, WorkSessionDto? activeOtherSession)
    {
        _shiftService = shiftService;
        _userId = userId;

        Text = "Bắt đầu ca làm việc";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(440, activeOtherSession is null ? 225 : 290);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        var y = 20;
        if (activeOtherSession is not null)
        {
            var warnPanel = new Panel
            {
                Location = new Point(20, y),
                Size = new Size(400, 62),
                BackColor = ColorTranslator.FromHtml("#FFF3E0"),
                BorderStyle = BorderStyle.FixedSingle,
                Padding = new Padding(10)
            };
            warnPanel.Controls.Add(new Label
            {
                Dock = DockStyle.Fill,
                ForeColor = CoffeeTheme.WarningOrange,
                Font = CoffeeTheme.SmallFont,
                Text = $"Phát hiện ca #{activeOtherSession.Id} của '{activeOtherSession.StaffName}' (mở lúc {activeOtherSession.StartTime:HH:mm}) chưa đóng. Khi xác nhận, hệ thống sẽ tự động chốt ca cũ với số tiền bàn giao này."
            });
            Controls.Add(warnPanel);
            y += 76;
        }

        var lbl = new Label
        {
            Text = "Tiền mặt đầu ca trong két (VNĐ):",
            Font = CoffeeTheme.BodyBoldFont,
            Location = new Point(20, y),
            Size = new Size(400, 24)
        };

        _numStartCash.Location = new Point(20, y + 28);
        _numStartCash.Size = new Size(400, 32);
        _numStartCash.Font = new Font("Segoe UI", 12F, FontStyle.Bold);
        _numStartCash.Maximum = 1_000_000_000m;
        _numStartCash.Increment = 50000m;
        _numStartCash.ThousandsSeparator = true;
        _numStartCash.Value = 0m;

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Để sau", 120, 38);
        btnCancel.Location = new Point(160, y + 86);
        btnCancel.Click += (_, _) =>
        {
            DialogResult = DialogResult.Cancel;
            Close();
        };

        _btnConfirm = CoffeeTheme.CreateSuccessButton("Xác nhận & Mở bán", 140, 38);
        _btnConfirm.Location = new Point(288, y + 86);
        _btnConfirm.Click += async (_, _) => await ConfirmAsync();

        Controls.AddRange(new Control[] { lbl, _numStartCash, btnCancel, _btnConfirm });
        AcceptButton = _btnConfirm;
        Shown += (_, _) =>
        {
            _numStartCash.Focus();
            _numStartCash.Select(0, _numStartCash.Text.Length);
        };
    }

    private async Task ConfirmAsync()
    {
        _btnConfirm.Enabled = false;
        var res = await _shiftService.StartShiftAsync(_userId, _numStartCash.Value);
        if (!res.Success || res.Data is null)
        {
            MessageBox.Show(res.Message, "Không thể mở ca", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            _btnConfirm.Enabled = true;
            return;
        }

        StartedSession = res.Data;
        MessageBox.Show(res.Message, "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
        DialogResult = DialogResult.OK;
        Close();
    }
}
