using System.Reflection;
using System.Runtime.InteropServices;

namespace CoffeeShop.WinForms.Theme;

public static partial class CoffeeTheme
{
    private const int WmSetRedraw = 0x000B;

    [DllImport("user32.dll")]
    private static extern nint SendMessage(nint hWnd, int msg, nint wParam, nint lParam);
    public static readonly Color Espresso = ColorTranslator.FromHtml("#3E2723");
    public static readonly Color Mocha = ColorTranslator.FromHtml("#6F4E37");
    public static readonly Color MochaHover = ColorTranslator.FromHtml("#5A3E2B");
    public static readonly Color Latte = ColorTranslator.FromHtml("#D7CCC8");
    public static readonly Color Cream = ColorTranslator.FromHtml("#F6F2EC");
    public static readonly Color Surface = Color.White;
    public static readonly Color Border = ColorTranslator.FromHtml("#E0D6CC");
    public static readonly Color TextPrimary = ColorTranslator.FromHtml("#2C1E1A");
    public static readonly Color TextMuted = ColorTranslator.FromHtml("#7A6A63");
    public static readonly Color AccentGreen = ColorTranslator.FromHtml("#2E7D32");
    public static readonly Color AccentBlue = ColorTranslator.FromHtml("#1565C0");
    public static readonly Color WarningOrange = ColorTranslator.FromHtml("#EF6C00");
    public static readonly Color DangerRed = ColorTranslator.FromHtml("#C62828");

    public static readonly Font TitleFont = new("Segoe UI", 16F, FontStyle.Bold);
    public static readonly Font SectionFont = new("Segoe UI", 11.5F, FontStyle.Bold);
    public static readonly Font BodyFont = new("Segoe UI", 9.5F, FontStyle.Regular);
    public static readonly Font BodyBoldFont = new("Segoe UI", 9.5F, FontStyle.Bold);
    public static readonly Font SmallFont = new("Segoe UI", 8.5F, FontStyle.Regular);

    public static Button CreatePrimaryButton(string text, int width = 130, int height = 36)
    {
        var btn = CreateBaseButton(text, Mocha, Color.White, width, height);
        btn.FlatAppearance.MouseOverBackColor = MochaHover;
        return btn;
    }

    public static Button CreateSuccessButton(string text, int width = 130, int height = 36)
    {
        var btn = CreateBaseButton(text, AccentGreen, Color.White, width, height);
        btn.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#1B5E20");
        return btn;
    }

    public static Button CreateDangerButton(string text, int width = 120, int height = 36)
    {
        var btn = CreateBaseButton(text, DangerRed, Color.White, width, height);
        btn.FlatAppearance.MouseOverBackColor = ColorTranslator.FromHtml("#8E0000");
        return btn;
    }

    public static Button CreateSecondaryButton(string text, int width = 120, int height = 36)
    {
        var btn = CreateBaseButton(text, Color.White, TextPrimary, width, height);
        btn.FlatAppearance.BorderSize = 1;
        btn.FlatAppearance.BorderColor = Border;
        btn.FlatAppearance.MouseOverBackColor = Cream;
        return btn;
    }

    private static Button CreateBaseButton(string text, Color back, Color fore, int width, int height)
    {
        var btn = new Button
        {
            Text = text,
            BackColor = back,
            ForeColor = fore,
            Font = BodyBoldFont,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Size = new Size(width, height),
            UseVisualStyleBackColor = false
        };
        btn.FlatAppearance.BorderSize = 0;
        return btn;
    }

    public static void StyleDataGridView(DataGridView grid)
    {
        EnableDoubleBuffering(grid);
        grid.BackgroundColor = Surface;
        grid.BorderStyle = BorderStyle.None;
        grid.CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal;
        grid.GridColor = Border;
        grid.EnableHeadersVisualStyles = false;
        grid.AllowUserToAddRows = false;
        grid.AllowUserToDeleteRows = false;
        grid.AllowUserToResizeRows = false;
        grid.RowHeadersVisible = false;
        grid.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        grid.MultiSelect = false;
        grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
        grid.ColumnHeadersHeight = 38;
        grid.RowTemplate.Height = 34;

        grid.ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Espresso,
            ForeColor = Color.White,
            SelectionBackColor = Espresso,
            SelectionForeColor = Color.White,
            Font = BodyBoldFont,
            Padding = new Padding(8, 0, 8, 0)
        };

        grid.DefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = Surface,
            ForeColor = TextPrimary,
            Font = BodyFont,
            SelectionBackColor = ColorTranslator.FromHtml("#EFEBE9"),
            SelectionForeColor = TextPrimary,
            Padding = new Padding(8, 0, 8, 0)
        };

        grid.AlternatingRowsDefaultCellStyle = new DataGridViewCellStyle
        {
            BackColor = ColorTranslator.FromHtml("#FAF7F4"),
            ForeColor = TextPrimary,
            Font = BodyFont,
            SelectionBackColor = ColorTranslator.FromHtml("#EFEBE9"),
            SelectionForeColor = TextPrimary,
            Padding = new Padding(8, 0, 8, 0)
        };
    }

    public static Panel CreateCardPanel(Padding? padding = null) => new()
    {
        BackColor = Surface,
        Padding = padding ?? new Padding(14),
        BorderStyle = BorderStyle.FixedSingle
    };

    public static Panel CreateKpiCard(string title, string initialValue, Color accentColor, out Label valueLabel)
    {
        var card = new Panel
        {
            BackColor = Surface,
            BorderStyle = BorderStyle.FixedSingle,
            Margin = new Padding(6),
            Padding = new Padding(14, 12, 14, 12),
            Dock = DockStyle.Fill
        };

        var bar = new Panel
        {
            BackColor = accentColor,
            Dock = DockStyle.Top,
            Height = 4
        };

        var lblTitle = new Label
        {
            Text = title.ToUpperInvariant(),
            Font = new Font("Segoe UI", 8.5F, FontStyle.Bold),
            ForeColor = TextMuted,
            Dock = DockStyle.Top,
            Height = 22,
            Padding = new Padding(0, 6, 0, 0)
        };

        valueLabel = new Label
        {
            Text = initialValue,
            Font = new Font("Segoe UI", 16F, FontStyle.Bold),
            ForeColor = accentColor,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft
        };

        card.Controls.Add(valueLabel);
        card.Controls.Add(lblTitle);
        card.Controls.Add(bar);
        return card;
    }

    public static SplitContainer CreateSafeSplitContainer(double initialRatio, int panel1MinSize, int panel2MinSize)
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Panel1MinSize = 0,
            Panel2MinSize = 0
        };

        var userAdjusted = false;
        var applying = false;

        void ApplyBounds(bool applyRatio)
        {
            if (applying || split.IsDisposed)
                return;

            var available = split.Width - split.SplitterWidth;
            if (available <= 4)
                return;

            applying = true;
            try
            {
                if (available >= panel1MinSize + panel2MinSize)
                {
                    split.Panel1MinSize = 0;
                    split.Panel2MinSize = 0;

                    var rawTarget = applyRatio && !userAdjusted
                        ? (int)Math.Round(split.Width * initialRatio)
                        : split.SplitterDistance;

                    var minAllowed = panel1MinSize;
                    var maxAllowed = Math.Max(minAllowed, available - panel2MinSize);
                    var target = Math.Clamp(rawTarget, minAllowed, maxAllowed);

                    split.SplitterDistance = target;
                    split.Panel1MinSize = panel1MinSize;
                    split.Panel2MinSize = panel2MinSize;
                }
                else
                {
                    split.Panel1MinSize = 0;
                    split.Panel2MinSize = 0;

                    var maxAllowed = Math.Max(1, available - 1);
                    var target = Math.Clamp((int)Math.Round(split.Width * initialRatio), 1, maxAllowed);
                    if (target >= 1 && target <= maxAllowed)
                        split.SplitterDistance = target;
                }
            }
            finally
            {
                applying = false;
            }
        }

        split.SplitterMoved += (_, _) =>
        {
            if (!applying && split.IsHandleCreated && split.Visible)
                userAdjusted = true;
        };
        split.HandleCreated += (_, _) => ApplyBounds(applyRatio: true);
        split.SizeChanged += (_, _) => ApplyBounds(applyRatio: !userAdjusted);

        return split;
    }

    public static void EnableDoubleBuffering(Control control)
    {
        try
        {
            typeof(Control)
                .GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.SetValue(control, true);
        }
        catch
        {
            // Safe fallback if runtime denies reflection on DoubleBuffered
        }
    }

    public static void SuspendDrawing(Control control)
    {
        if (control.IsHandleCreated && !control.IsDisposed)
            SendMessage(control.Handle, WmSetRedraw, 0, 0);
    }

    public static void ResumeDrawing(Control control)
    {
        if (!control.IsHandleCreated || control.IsDisposed)
            return;

        SendMessage(control.Handle, WmSetRedraw, 1, 0);
        control.Invalidate(true);
        control.Update();
    }
}
