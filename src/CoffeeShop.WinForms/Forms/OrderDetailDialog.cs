using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Domain.Enums;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class OrderDetailDialog : Form
{
    public OrderDetailDialog(OrderDto order, AppSettings? settings = null)
    {
        var effectiveSettings = settings ?? new AppSettings();
        Text = $"Chi tiết đơn hàng {order.OrderCode}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(640, 480);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        var infoPanel = new Panel
        {
            Dock = DockStyle.Top,
            Height = 115,
            BackColor = CoffeeTheme.Cream,
            Padding = new Padding(16)
        };

        var statusLine = order.Status == OrderStatus.Cancelled
            ? $"Trạng thái: ĐÃ HỦY ({order.CancelledAt:dd/MM/yyyy HH:mm} bởi {order.CancelledByName}) - Lý do: {order.CancelReason}"
            : "Trạng thái: ĐÃ THANH TOÁN";

        infoPanel.Controls.Add(new Label
        {
            Dock = DockStyle.Fill,
            Font = CoffeeTheme.BodyFont,
            Text =
                $"Mã đơn: {order.OrderCode}   |   Thời gian: {order.OrderDate:dd/MM/yyyy HH:mm:ss}\n" +
                $"Thu ngân: {order.StaffName}   |   Phương thức: {FormatHelper.PaymentLabel(order.PaymentMethod)}\n" +
                $"Voucher: {(string.IsNullOrEmpty(order.VoucherCode) ? "Không" : $"{order.VoucherCode} ({order.DiscountPercent:0.##}%)")}\n" +
                statusLine
        });

        var grid = new DataGridView { Dock = DockStyle.Fill, ReadOnly = true };
        CoffeeTheme.StyleDataGridView(grid);
        grid.Columns.Add("ProductName", "Tên món");
        grid.Columns.Add("Quantity", "SL");
        grid.Columns.Add("UnitPrice", "Đơn giá");
        grid.Columns.Add("LineTotal", "Thành tiền");
        grid.Columns.Add("Note", "Ghi chú");

        grid.Columns["Quantity"]!.FillWeight = 40;
        grid.Columns["UnitPrice"]!.FillWeight = 75;
        grid.Columns["LineTotal"]!.FillWeight = 85;

        foreach (var item in order.Items)
        {
            grid.Rows.Add(
                item.ProductName,
                item.Quantity,
                FormatHelper.Currency(item.UnitPrice),
                FormatHelper.Currency(item.LineTotal),
                item.Note ?? string.Empty);
        }

        var footer = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 68,
            BackColor = CoffeeTheme.Surface,
            Padding = new Padding(16, 12, 16, 12)
        };

        var lblTotals = new Label
        {
            Dock = DockStyle.Fill,
            Font = CoffeeTheme.BodyBoldFont,
            ForeColor = CoffeeTheme.Espresso,
            TextAlign = ContentAlignment.MiddleLeft,
            Text = $"Tạm tính: {FormatHelper.Currency(order.Subtotal)}  |  Giảm: -{FormatHelper.Currency(order.DiscountAmount)}  |  THỰC THU: {FormatHelper.Currency(order.FinalAmount)}"
        };

        var btnPrint = CoffeeTheme.CreateSuccessButton("In hóa đơn", 112, 36);
        btnPrint.Dock = DockStyle.Right;
        btnPrint.Click += (_, _) => ReceiptPrintHelper.PrintReceipt(this, order, effectiveSettings);

        var spacer = new Panel { Dock = DockStyle.Right, Width = 8 };

        var btnClose = CoffeeTheme.CreatePrimaryButton("Đóng", 100, 36);
        btnClose.Dock = DockStyle.Right;
        btnClose.Click += (_, _) => Close();

        footer.Controls.Add(lblTotals);
        footer.Controls.Add(btnPrint);
        footer.Controls.Add(spacer);
        footer.Controls.Add(btnClose);

        Controls.Add(grid);
        Controls.Add(footer);
        Controls.Add(infoPanel);
        AcceptButton = btnClose;
    }
}
