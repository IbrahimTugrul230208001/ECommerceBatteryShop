namespace ECommerceBatteryShop.Models;

public class CheckoutPageViewModel
{
    public decimal SubTotal { get; set; }
    public decimal ShippingCost { get; set; } = 150m;
    public IReadOnlyList<AddressViewModel> Addresses { get; set; } = Array.Empty<AddressViewModel>();
    public string? IyzipayCheckoutFormContent { get; set; }
}
