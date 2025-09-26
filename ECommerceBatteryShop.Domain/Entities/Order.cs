namespace ECommerceBatteryShop.Domain.Entities;


public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User? User { get; set; }
    public int AddressId { get; set; }
    public Address? Address { get; set; }
    public string? Status { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();
    public Shipment? Shipment { get; set; }
}
