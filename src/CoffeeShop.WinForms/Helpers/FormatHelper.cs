using CoffeeShop.Domain.Enums;

namespace CoffeeShop.WinForms.Helpers;

public static class FormatHelper
{
    public static string Currency(decimal amount) => $"{amount:N0} đ";

    public static string RoleLabel(UserRole role) => role switch
    {
        UserRole.Admin => "Quản lý (Admin)",
        UserRole.Staff => "Thu ngân / Phục vụ",
        UserRole.WarehouseStaff => "Thủ kho",
        _ => role.ToString()
    };

    public static string ShiftLabel(ShiftType shift) => shift switch
    {
        ShiftType.Morning => "Ca Sáng (07:00 - 12:00)",
        ShiftType.Afternoon => "Ca Chiều (12:00 - 17:00)",
        ShiftType.Evening => "Ca Tối (17:00 - 23:30)",
        _ => shift.ToString()
    };

    public static string PaymentLabel(PaymentMethod method) => method switch
    {
        PaymentMethod.Cash => "Tiền mặt",
        PaymentMethod.Transfer => "Chuyển khoản (QR)",
        _ => method.ToString()
    };

    public static string OrderStatusLabel(OrderStatus status) => status switch
    {
        OrderStatus.Paid => "Đã thanh toán",
        OrderStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    public static string InventoryTxLabel(InventoryTransactionType type) => type switch
    {
        InventoryTransactionType.Import => "Nhập kho",
        InventoryTransactionType.SaleExport => "Xuất bán hàng",
        InventoryTransactionType.ManualExport => "Xuất điều chỉnh / Hủy",
        InventoryTransactionType.CancelRestock => "Hoàn kho hủy đơn",
        InventoryTransactionType.Adjustment => "Kiểm kê",
        _ => type.ToString()
    };
}
