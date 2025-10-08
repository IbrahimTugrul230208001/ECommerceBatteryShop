using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models;

public class PlaceOrderInputModel
{
    [Required]
    public string PaymentMethod { get; set; } = string.Empty;

    // Card fields (for card_new)
    public string? Name { get; set; }
    public string? Number { get; set; }
    public string? Exp { get; set; }
    public string? Cvc { get; set; }

    // Saved card selection (for card_saved)
    public string? CardId { get; set; }

    public bool Save { get; set; }

    // Shipping selection
    public string? ShippingId { get; set; }
    public decimal? ShippingPrice { get; set; }

    // Guest checkout fields (used when user is not authenticated)
    public string? GuestName { get; set; }
    public string? GuestSurname { get; set; }
    public string? GuestEmail { get; set; }
    public string? GuestPhone { get; set; }
    public string? GuestCity { get; set; }
    public string? GuestState { get; set; }
    public string? GuestNeighbourhood { get; set; }
    public string? GuestFullAddress { get; set; }
}
