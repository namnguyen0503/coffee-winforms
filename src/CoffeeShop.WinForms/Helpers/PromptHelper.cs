using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Helpers;

public static class PromptHelper
{
    public static string? ShowTextInput(IWin32Window? owner, string title, string prompt, string defaultValue = "", bool required = true)
    {
        using var dlg = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(420, 170),
            BackColor = CoffeeTheme.Surface,
            Font = CoffeeTheme.BodyFont
        };

        var lbl = new Label
        {
            Text = prompt,
            Font = CoffeeTheme.BodyBoldFont,
            Location = new Point(18, 16),
            Size = new Size(384, 24)
        };

        var txt = new TextBox
        {
            Location = new Point(18, 46),
            Size = new Size(384, 30),
            Font = CoffeeTheme.BodyFont,
            Text = defaultValue
        };

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Hủy", 100, 36);
        btnCancel.Location = new Point(194, 112);
        btnCancel.Click += (_, _) =>
        {
            dlg.DialogResult = DialogResult.Cancel;
            dlg.Close();
        };

        var btnOk = CoffeeTheme.CreatePrimaryButton("Xác nhận", 100, 36);
        btnOk.Location = new Point(302, 112);
        btnOk.Click += (_, _) =>
        {
            if (required && string.IsNullOrWhiteSpace(txt.Text))
            {
                MessageBox.Show("Vui lòng nhập nội dung.", "Thiếu thông tin", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                txt.Focus();
                return;
            }
            dlg.DialogResult = DialogResult.OK;
            dlg.Close();
        };

        dlg.Controls.AddRange(new Control[] { lbl, txt, btnCancel, btnOk });
        dlg.AcceptButton = btnOk;
        dlg.CancelButton = btnCancel;

        return (owner is null ? dlg.ShowDialog() : dlg.ShowDialog(owner)) == DialogResult.OK ? txt.Text.Trim() : null;
    }

    public static decimal? ShowPercentInput(IWin32Window? owner, string title, string prompt, decimal defaultPercent = 100m)
    {
        using var dlg = new Form
        {
            Text = title,
            StartPosition = FormStartPosition.CenterParent,
            FormBorderStyle = FormBorderStyle.FixedDialog,
            MaximizeBox = false,
            MinimizeBox = false,
            ClientSize = new Size(360, 168),
            BackColor = CoffeeTheme.Surface,
            Font = CoffeeTheme.BodyFont
        };

        var lbl = new Label
        {
            Text = prompt,
            Font = CoffeeTheme.BodyBoldFont,
            Location = new Point(18, 16),
            Size = new Size(324, 24)
        };

        var num = new NumericUpDown
        {
            Location = new Point(18, 46),
            Size = new Size(324, 30),
            Minimum = 0m,
            Maximum = 100m,
            DecimalPlaces = 2,
            Value = Math.Clamp(defaultPercent, 0m, 100m),
            Font = new Font("Segoe UI", 11F, FontStyle.Bold)
        };

        var btnCancel = CoffeeTheme.CreateSecondaryButton("Hủy", 100, 36);
        btnCancel.Location = new Point(134, 110);
        btnCancel.Click += (_, _) =>
        {
            dlg.DialogResult = DialogResult.Cancel;
            dlg.Close();
        };

        var btnOk = CoffeeTheme.CreatePrimaryButton("Áp dụng", 100, 36);
        btnOk.Location = new Point(242, 110);
        btnOk.Click += (_, _) =>
        {
            dlg.DialogResult = DialogResult.OK;
            dlg.Close();
        };

        dlg.Controls.AddRange(new Control[] { lbl, num, btnCancel, btnOk });
        dlg.AcceptButton = btnOk;

        return (owner is null ? dlg.ShowDialog() : dlg.ShowDialog(owner)) == DialogResult.OK ? num.Value : null;
    }
}
