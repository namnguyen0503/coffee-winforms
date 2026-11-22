using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Domain.Entities;

public class Order
{
    public int Id { get; set; }
    public string OrderCode { get; set; } = string.Empty;
    public int UserId { get; set; }
    public int? WorkSessionId { get; set; }
    public DateTime OrderDate { get; set; } = DateTime.Now;
    public decimal Subtotal { get; set; }
    public string? VoucherCode { get; set; }
    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal FinalAmount { get; set; }
    public PaymentMethod PaymentMethod { get; set; } = PaymentMethod.Cash;
    public OrderStatus Status { get; set; } = OrderStatus.Paid;
    public int? CancelledByUserId { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }

    public User? User { get; set; }
    public User? CancelledByUser { get; set; }
    public WorkSession? WorkSession { get; set; }
    public ICollection<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
