using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models;

public class PlaceOrderInputModel
{
    [Required]
    public string PaymentMethod { get; set; } = string.Empty;

    public string? Name { get; set; }

    public string? Number { get; set; }

    public string? Exp { get; set; }

    public string? Cvc { get; set; }

    public string? CardId { get; set; }

    public bool Save { get; set; }
}
