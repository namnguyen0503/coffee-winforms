using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Domain.Entities;

public class CashTransaction
{
    public int Id { get; set; }
    public int? WorkSessionId { get; set; }
    public int UserId { get; set; }
    public CashTransactionType Type { get; set; } = CashTransactionType.AdminWithdrawal;
    public decimal Amount { get; set; }
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public WorkSession? WorkSession { get; set; }
    public User? User { get; set; }
}
