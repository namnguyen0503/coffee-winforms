using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Domain.Entities;

public class WorkSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime StartTime { get; set; } = DateTime.Now;
    public DateTime? EndTime { get; set; }
    public decimal StartCash { get; set; }
    public decimal EndCash { get; set; }
    public decimal CashSales { get; set; }
    public decimal TransferSales { get; set; }
    public decimal TotalSales { get; set; }
    public decimal ExpectedCash { get; set; }
    public decimal CashVariance { get; set; }
    public string? Note { get; set; }
    public WorkSessionStatus Status { get; set; } = WorkSessionStatus.Open;

    public User? User { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
