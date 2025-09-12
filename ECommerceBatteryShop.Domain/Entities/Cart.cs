namespace ECommerceBatteryShop.Domain.Entities;
public class Cart
{
    public int Id { get; set; }

    // either UserId (for logged-in) OR AnonId (for guest) will be set
    public int? UserId { get; set; }
    public User? User { get; set; }

    public string? AnonId { get; set; }   // <-- add this
    public DateTime CreatedAt { get; set; }

    public ICollection<CartItem> Items { get; set; } = new List<CartItem>();
}
