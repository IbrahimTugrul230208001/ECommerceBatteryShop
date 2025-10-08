namespace ECommerceBatteryShop.Models;

public class CheckoutPageViewModel
{
    public decimal SubTotal { get; set; }
    public decimal ShippingCost { get; set; } = 150m;
    public IReadOnlyList<AddressViewModel> Addresses { get; set; } = Array.Empty<AddressViewModel>();

    // For guests: carry data captured on MisafirController to the checkout page
    public bool IsGuest { get; set; }
    public GuestCheckoutViewModel? Guest { get; set; }
}
