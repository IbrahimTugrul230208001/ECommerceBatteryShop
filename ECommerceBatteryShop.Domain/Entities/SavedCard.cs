namespace ECommerceBatteryShop.Domain.Entities;

public class SavedCard
{
    public int Id { get; set; } // EF configured as identity/serial
    public int UserId { get; set; }
    public User? User { get; set; }

    public string CardUserKey { get; set; } = string.Empty;
    public string CardToken { get; set; } = string.Empty;

    public string Brand { get; set; } = string.Empty;
    public string Last4 { get; set; } = string.Empty;
    public string Holder { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
