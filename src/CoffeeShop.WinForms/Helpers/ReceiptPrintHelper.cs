using System.Drawing.Printing;
using CoffeeShop.Application.Common;
using CoffeeShop.Application.DTOs;

namespace CoffeeShop.WinForms.Helpers;

public static class ReceiptPrintHelper
{
    public static PrintDocument BuildReceiptDocument(OrderDto order, AppSettings settings)
    {
        var doc = new PrintDocument
        {
            DocumentName = $"HoaDon_{order.OrderCode}"
        };
        doc.DefaultPageSettings.PaperSize = new PaperSize("Receipt80mm", 300, 1100);
        doc.DefaultPageSettings.Margins = new Margins(12, 12, 14, 14);

        int itemIndex = 0;
        bool headerDrawn = false;
        bool footerDrawn = false;

        doc.BeginPrint += (_, _) =>
        {
            itemIndex = 0;
            headerDrawn = false;
            footerDrawn = false;
        };

        doc.PrintPage += (_, e) =>
        {
            var g = e.Graphics!;
            var bounds = e.MarginBounds.Width > 50 ? e.MarginBounds : new Rectangle(12, 14, Math.Max(200, e.PageBounds.Width - 24), Math.Max(200, e.PageBounds.Height - 28));
            float x = bounds.Left;
            float width = bounds.Width;
            float y = bounds.Top;
            float bottom = bounds.Bottom;

            using var fontTitle = new Font("Segoe UI", 12F, FontStyle.Bold);
            using var fontBold = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            using var fontRegular = new Font("Segoe UI", 8.5F, FontStyle.Regular);
            using var fontItalic = new Font("Segoe UI", 7.5F, FontStyle.Italic);
            using var fontTotal = new Font("Segoe UI", 10.5F, FontStyle.Bold);
            using var brush = new SolidBrush(Color.Black);
            using var penDashed = new Pen(Color.Black, 1f) { DashPattern = [3f, 2f] };

            var sfCenter = new StringFormat { Alignment = StringAlignment.Center };
            var sfLeft = new StringFormat { Alignment = StringAlignment.Near, Trimming = StringTrimming.EllipsisCharacter };
            var sfRight = new StringFormat { Alignment = StringAlignment.Far };

            void DrawCentered(string text, Font f, float extraGap = 2f)
            {
                var size = g.MeasureString(text, f, (int)width);
                g.DrawString(text, f, brush, new RectangleF(x, y, width, size.Height + 2), sfCenter);
                y += size.Height + extraGap;
            }

            void DrawRow(string left, string right, Font f, float extraGap = 2f)
            {
                var h = Math.Max(g.MeasureString(left, f).Height, g.MeasureString(right, f).Height);
                g.DrawString(left, f, brush, new RectangleF(x, y, width * 0.65f, h + 2), sfLeft);
                g.DrawString(right, f, brush, new RectangleF(x, y, width, h + 2), sfRight);
                y += h + extraGap;
            }

            void DrawDivider(float gap = 5f)
            {
                y += 2f;
                g.DrawLine(penDashed, x, y, x + width, y);
                y += gap;
            }

            if (!headerDrawn)
            {
                DrawCentered(settings.StoreName.ToUpperInvariant(), fontTitle, 2f);
                DrawCentered(settings.StoreAddress, fontRegular, 4f);
                DrawDivider();
                DrawCentered("HÓA ĐƠN THANH TOÁN", fontBold, 4f);
                DrawRow("Mã HĐ:", order.OrderCode, fontRegular);
                DrawRow("Ngày giờ:", order.OrderDate.ToString("dd/MM/yyyy HH:mm:ss"), fontRegular);
                DrawRow("Thu ngân:", order.StaffName, fontRegular);
                DrawRow("Thanh toán:", FormatHelper.PaymentLabel(order.PaymentMethod), fontRegular);
                DrawDivider();
                headerDrawn = true;
            }

            while (itemIndex < order.Items.Count)
            {
                var item = order.Items[itemIndex];
                var estHeight = string.IsNullOrWhiteSpace(item.Note) ? 38f : 52f;
                if (y + estHeight > bottom - 20f)
                {
                    e.HasMorePages = true;
                    return;
                }

                var nameSize = g.MeasureString(item.ProductName, fontBold, (int)width);
                g.DrawString(item.ProductName, fontBold, brush, new RectangleF(x, y, width, nameSize.Height + 2), sfLeft);
                y += nameSize.Height + 1f;

                DrawRow($"  {item.Quantity} x {item.UnitPrice:N0} đ", $"{item.LineTotal:N0} đ", fontRegular, 1f);

                if (!string.IsNullOrWhiteSpace(item.Note))
                {
                    var noteText = $"  Ghi chú: {item.Note}";
                    var noteSize = g.MeasureString(noteText, fontItalic, (int)width);
                    g.DrawString(noteText, fontItalic, brush, new RectangleF(x, y, width, noteSize.Height + 2), sfLeft);
                    y += noteSize.Height + 2f;
                }

                itemIndex++;
            }

            if (!footerDrawn)
            {
                if (y + 115f > bottom)
                {
                    e.HasMorePages = true;
                    return;
                }

                DrawDivider();
                DrawRow("Tạm tính:", FormatHelper.Currency(order.Subtotal), fontRegular);
                if (order.DiscountPercent > 0)
                    DrawRow($"Giảm ({order.VoucherCode} {order.DiscountPercent:0.##}%):", $"-{FormatHelper.Currency(order.DiscountAmount)}", fontRegular);

                DrawRow("THÀNH TIỀN:", FormatHelper.Currency(order.FinalAmount), fontTotal, 4f);
                DrawDivider(6f);
                DrawCentered(settings.WifiInfo, fontItalic, 3f);
                DrawCentered("Cảm ơn quý khách & Hẹn gặp lại!", fontBold, 2f);
                footerDrawn = true;
            }

            e.HasMorePages = false;
        };

        return doc;
    }

    public static PrintDocument BuildStickerDocument(OrderDto order)
    {
        var stickers = new List<(string OrderCode, DateTime OrderDate, int Index, int Total, string ProductName, string? Note, string StaffName)>();
        foreach (var item in order.Items)
        {
            for (int i = 1; i <= item.Quantity; i++)
                stickers.Add((order.OrderCode, order.OrderDate, i, item.Quantity, item.ProductName, item.Note, order.StaffName));
        }

        var doc = new PrintDocument
        {
            DocumentName = $"TemCoc_{order.OrderCode}"
        };
        doc.DefaultPageSettings.PaperSize = new PaperSize("CupSticker", 200, 120);
        doc.DefaultPageSettings.Margins = new Margins(8, 8, 8, 8);

        int idx = 0;
        doc.BeginPrint += (_, _) => idx = 0;
        doc.PrintPage += (_, e) =>
        {
            if (idx >= stickers.Count)
            {
                e.HasMorePages = false;
                return;
            }

            var st = stickers[idx++];
            var g = e.Graphics!;
            var bounds = e.MarginBounds.Width > 30 ? e.MarginBounds : new Rectangle(8, 8, Math.Max(140, e.PageBounds.Width - 16), Math.Max(80, e.PageBounds.Height - 16));
            float x = bounds.Left;
            float y = bounds.Top;
            float width = bounds.Width;

            using var fontSmall = new Font("Segoe UI", 7.5F, FontStyle.Regular);
            using var fontBold = new Font("Segoe UI", 10F, FontStyle.Bold);
            using var fontNote = new Font("Segoe UI", 8F, FontStyle.Italic);
            using var brush = new SolidBrush(Color.Black);

            var shortCode = st.OrderCode.Length > 8 ? st.OrderCode[^8..] : st.OrderCode;
            g.DrawString($"#{shortCode}  {st.OrderDate:HH:mm}   ({st.Index}/{st.Total})", fontSmall, brush, new RectangleF(x, y, width, 16));
            y += 17f;

            var nameSize = g.MeasureString(st.ProductName, fontBold, (int)width);
            g.DrawString(st.ProductName, fontBold, brush, new RectangleF(x, y, width, nameSize.Height + 2));
            y += nameSize.Height + 2f;

            if (!string.IsNullOrWhiteSpace(st.Note))
            {
                var noteSize = g.MeasureString(st.Note, fontNote, (int)width);
                g.DrawString(st.Note, fontNote, brush, new RectangleF(x, y, width, noteSize.Height + 2));
                y += noteSize.Height + 2f;
            }

            g.DrawString($"NV: {st.StaffName}", fontSmall, brush, new RectangleF(x, y, width, 16));
            e.HasMorePages = idx < stickers.Count;
        };

        return doc;
    }

    public static void PrintReceipt(IWin32Window? owner, OrderDto order, AppSettings settings)
    {
        using var doc = BuildReceiptDocument(order, settings);
        RunPrintDialog(owner, doc, "In hóa đơn");
    }

    public static void ShowReceiptPreview(IWin32Window? owner, OrderDto order, AppSettings settings)
    {
        try
        {
            using var doc = BuildReceiptDocument(order, settings);
            using var preview = new PrintPreviewDialog
            {
                Document = doc,
                Text = $"Xem trước hóa đơn {order.OrderCode}",
                Width = 540,
                Height = 700,
                StartPosition = FormStartPosition.CenterParent
            };
            if (owner is null) preview.ShowDialog();
            else preview.ShowDialog(owner);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể mở xem trước bản in: {ex.Message}", "Lỗi in ấn", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    public static void PrintStickers(IWin32Window? owner, OrderDto order)
    {
        if (order.Items.Count == 0)
        {
            MessageBox.Show("Đơn hàng không có món để in tem.", "Thông báo", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        using var doc = BuildStickerDocument(order);
        RunPrintDialog(owner, doc, "In tem dán cốc");
    }

    private static void RunPrintDialog(IWin32Window? owner, PrintDocument doc, string title)
    {
        try
        {
            using var dlg = new PrintDialog
            {
                Document = doc,
                UseEXDialog = true
            };
            var res = owner is null ? dlg.ShowDialog() : dlg.ShowDialog(owner);
            if (res == DialogResult.OK)
                doc.Print();
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Không thể {title.ToLowerInvariant()}: {ex.Message}", "Lỗi máy in", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }
}
