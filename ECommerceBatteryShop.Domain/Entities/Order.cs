namespace ECommerceBatteryShop.Domain.Entities;


public class Order
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    // Buyer identity (nullable for guests)
    public int? UserId { get; set; }            // nullable for guests
    public User? User { get; set; }

    // Saved address reference if a logged-in user chose an existing address
    public int? AddressId { get; set; }         // nullable for guests or edited one-off address
    public Address? Address { get; set; }

    // Guest correlation (optional): stores ANON_ID cookie value to tie carts/orders
    public string? AnonId { get; set; }

    // Immutable buyer and shipping snapshot captured at checkout time
    // Always populate these for both logged-in and guest orders
    [System.ComponentModel.DataAnnotations.MaxLength(256)]
    public string? BuyerName { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(256)]
    public string? BuyerEmail { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(32)]
    public string?   BuyerPhone { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(512)]
    public string? ShippingAddressText { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string? ShippingCity { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(128)]
    public string? ShippingState { get; set; }

    [System.ComponentModel.DataAnnotations.MaxLength(256)]
    public string? ShippingNeighbourhood { get; set; }

    public string? Status { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }

    public ICollection<OrderItem> Items { get; set; } = new List<OrderItem>();
    public ICollection<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();
    public Shipment? Shipment { get; set; }
}
