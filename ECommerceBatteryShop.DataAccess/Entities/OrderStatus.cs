namespace ECommerceBatteryShop.DataAccess.Entities;

public class OrderStatus
{
    public int Id { get; set; }
    public string Status { get; set; } = string.Empty;
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
