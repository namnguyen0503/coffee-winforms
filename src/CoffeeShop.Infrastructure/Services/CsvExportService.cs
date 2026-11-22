using System.Globalization;
using System.Text;
using CoffeeShop.Application.DTOs;
using CoffeeShop.Application.Interfaces;

namespace CoffeeShop.Infrastructure.Services;

public class CsvExportService : ICsvExportService
{
    public void ExportOrders(string filePath, IReadOnlyList<OrderDto> orders)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MaDon,ThoiGian,NhanVien,PhuongThuc,TrangThai,TamTinh,Voucher,GiamGia,ThucThu,LyDoHuy");
        foreach (var o in orders)
        {
            sb.AppendLine(string.Join(",",
                Escape(o.OrderCode),
                Escape(o.OrderDate.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)),
                Escape(o.StaffName),
                Escape(o.PaymentMethod.ToString()),
                Escape(o.Status.ToString()),
                Fmt(o.Subtotal),
                Escape(o.VoucherCode ?? ""),
                Fmt(o.DiscountAmount),
                Fmt(o.FinalAmount),
                Escape(o.CancelReason ?? "")));
        }

        WriteUtf8Bom(filePath, sb.ToString());
    }

    public void ExportFinancialReport(string filePath, FinancialReportDto report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MaDon,ThoiGian,NhanVien,PhuongThuc,DoanhThu,GiaVon,LoiNhuan");
        foreach (var row in report.Orders)
        {
            sb.AppendLine(string.Join(",",
                Escape(row.OrderCode),
                Escape(row.OrderDate.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)),
                Escape(row.StaffName),
                Escape(row.PaymentMethodLabel),
                Fmt(row.Revenue),
                Fmt(row.CostOfGoodsSold),
                Fmt(row.GrossProfit)));
        }

        sb.AppendLine();
        sb.AppendLine(FormattableString.Invariant($"TONG DOANH THU,,,,{report.TotalRevenue:0.##},{report.TotalCogs:0.##},{report.GrossProfit:0.##}"));
        WriteUtf8Bom(filePath, sb.ToString());
    }

    public void ExportInventoryLogs(string filePath, IReadOnlyList<InventoryTransactionDto> logs)
    {
        var sb = new StringBuilder();
        sb.AppendLine("ID,ThoiGian,NguyenLieu,DonVi,LoaiGiaoDich,SoLuong,DonGia,ThanhTien,NguoiThucHien,GhiChu");
        foreach (var l in logs)
        {
            sb.AppendLine(string.Join(",",
                l.Id.ToString(CultureInfo.InvariantCulture),
                Escape(l.CreatedAt.ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)),
                Escape(l.IngredientName),
                Escape(l.Unit),
                Escape(l.Type.ToString()),
                l.Quantity.ToString("0.###", CultureInfo.InvariantCulture),
                Fmt(l.UnitCost),
                Fmt(l.TotalCost),
                Escape(l.PerformedBy),
                Escape(l.Note ?? "")));
        }

        WriteUtf8Bom(filePath, sb.ToString());
    }

    public void ExportCashFlow(string filePath, IReadOnlyList<CashFlowRowDto> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("MaCa,NhanVien,BatDau,KetThuc,TrangThai,CuoiCaTruoc,TienDauCa,LechBanGiao,TienMatBanHang,ChuyenKhoanBanHang,TongDoanhThu,AdminRutKet,TienMatKyVong,TienCuoiCa,LechKiemKe,GhiChu");
        foreach (var r in rows)
        {
            sb.AppendLine(string.Join(",",
                r.SessionId.ToString(CultureInfo.InvariantCulture),
                Escape(r.StaffName),
                Escape(r.StartTime.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture)),
                Escape(r.EndTime?.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture) ?? "Đang mở"),
                Escape(r.Status.ToString()),
                r.PreviousEndCash.HasValue ? Fmt(r.PreviousEndCash.Value) : "",
                Fmt(r.StartCash),
                Fmt(r.HandoverDiff),
                Fmt(r.CashSales),
                Fmt(r.TransferSales),
                Fmt(r.TotalSales),
                Fmt(r.CashWithdrawn),
                Fmt(r.ExpectedCash),
                Fmt(r.EndCash),
                Fmt(r.CashVariance),
                Escape(r.Note ?? "")));
        }

        WriteUtf8Bom(filePath, sb.ToString());
    }

    private static string Fmt(decimal value) => value.ToString("0.##", CultureInfo.InvariantCulture);

    private static string Escape(string value)
    {
        var escaped = value.Replace("\"", "\"\"");
        return $"\"{escaped}\"";
    }

    private static void WriteUtf8Bom(string filePath, string content)
    {
        File.WriteAllText(filePath, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: true));
    }
}
