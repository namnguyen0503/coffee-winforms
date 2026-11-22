using System.Text;
using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;
using CoffeeShop.WinForms.Helpers;
using CoffeeShop.WinForms.Theme;

namespace CoffeeShop.WinForms.Forms;

public class ReceiptDialog : Form
{
    private readonly OrderDto _order;
    private readonly AppSettings _settings;

    public ReceiptDialog(OrderDto order, AppSettings settings)
    {
        _order = order;
        _settings = settings;

        Text = $"Hóa đơn {order.OrderCode}";
        StartPosition = FormStartPosition.CenterParent;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ClientSize = new Size(460, 560);
        BackColor = CoffeeTheme.Surface;
        Font = CoffeeTheme.BodyFont;

        var sb = new StringBuilder();
        sb.AppendLine($"         {settings.StoreName.ToUpperInvariant()}");
        sb.AppendLine($"      {settings.StoreAddress}");
        sb.AppendLine("==========================================");
        sb.AppendLine($"Mã hóa đơn : {order.OrderCode}");
        sb.AppendLine($"Thời gian  : {order.OrderDate:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Thu ngân   : {order.StaffName}");
        sb.AppendLine($"Thanh toán : {FormatHelper.PaymentLabel(order.PaymentMethod)}");
        sb.AppendLine("------------------------------------------");
        foreach (var item in order.Items)
        {
            sb.AppendLine($"{item.ProductName}");
            sb.AppendLine($"  {item.Quantity} x {item.UnitPrice:N0}đ = {item.LineTotal:N0}đ");
            if (!string.IsNullOrWhiteSpace(item.Note))
                sb.AppendLine($"  Ghi chú: {item.Note}");
        }
        sb.AppendLine("------------------------------------------");
        sb.AppendLine($"Tạm tính   : {FormatHelper.Currency(order.Subtotal)}");
        if (order.DiscountPercent > 0)
            sb.AppendLine($"Giảm ({order.VoucherCode} {order.DiscountPercent:0.##}%): -{FormatHelper.Currency(order.DiscountAmount)}");
        sb.AppendLine($"THÀNH TIỀN : {FormatHelper.Currency(order.FinalAmount)}");
        sb.AppendLine("==========================================");
        sb.AppendLine($"   {settings.WifiInfo}");
        sb.AppendLine("     Cảm ơn quý khách & Hẹn gặp lại!");

        var txtReceipt = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Font = new Font("Consolas", 9.5F),
            BackColor = CoffeeTheme.Cream,
            BorderStyle = BorderStyle.FixedSingle,
            Location = new Point(16, 16),
            Size = new Size(428, 476),
            Text = sb.ToString()
        };

        var btnPrint = CoffeeTheme.CreateSuccessButton("In hóa đơn", 112, 36);
        btnPrint.Location = new Point(16, 506);
        btnPrint.Click += (_, _) => ReceiptPrintHelper.PrintReceipt(this, _order, _settings);

        var btnPreview = CoffeeTheme.CreateSecondaryButton("Xem trước", 96, 36);
        btnPreview.Location = new Point(134, 506);
        btnPreview.Click += (_, _) => ReceiptPrintHelper.ShowReceiptPreview(this, _order, _settings);

        var btnSticker = CoffeeTheme.CreateSecondaryButton("In tem cốc", 104, 36);
        btnSticker.Location = new Point(236, 506);
        btnSticker.Click += (_, _) => ReceiptPrintHelper.PrintStickers(this, _order);

        var btnClose = CoffeeTheme.CreatePrimaryButton("Đóng", 98, 36);
        btnClose.Location = new Point(346, 506);
        btnClose.Click += (_, _) => Close();

        Controls.AddRange(new Control[] { txtReceipt, btnPrint, btnPreview, btnSticker, btnClose });
        AcceptButton = btnClose;
    }
}
