namespace ECommerceBatteryShop.Domain.Entities;

public class Address
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string? State { get; set; }
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
