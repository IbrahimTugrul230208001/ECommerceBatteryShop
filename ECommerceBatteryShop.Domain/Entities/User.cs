namespace ECommerceBatteryShop.DataAccess.Entities;

public class User
{
    public int Id { get; set; }
    public string? UserName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public ICollection<Address> Addresses { get; set; } = new List<Address>();
    public ICollection<Cart> Carts { get; set; } = new List<Cart>();
    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
