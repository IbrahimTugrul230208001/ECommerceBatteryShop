namespace ECommerceBatteryShop.Domain.Entities;


public class Order
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    public int? UserId { get; set; }            // nullable for guests
    public User? User { get; set; }

    public int? AddressId { get; set; }         // nullable for guests
    public Address? Address { get; set; }

    public string? AnonId { get; set; }         // optional: correlate guest orders to ANON_ID

    // Snapshot for all orders (users and guests)
    public string BuyerName { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string BuyerPhone { get; set; } = string.Empty;
    public string ShippingAddressText { get; set; } = string.Empty;
    public string ShippingCity { get; set; } = string.Empty;
    public string ShippingState { get; set; } = string.Empty;
    public string ShippingNeighbourhood { get; set; } = string.Empty;

    public string? Status { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();
    public Shipment? Shipment { get; set; }
}
