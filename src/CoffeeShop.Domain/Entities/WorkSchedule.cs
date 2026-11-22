using CoffeeShop.Domain.Enums;

namespace CoffeeShop.Domain.Entities;

public class WorkSchedule
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateOnly ShiftDate { get; set; }
    public ShiftType ShiftType { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.Now;

    public User? User { get; set; }
}
