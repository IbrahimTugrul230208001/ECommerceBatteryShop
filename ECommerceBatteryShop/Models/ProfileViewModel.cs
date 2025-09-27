namespace ECommerceBatteryShop.Models;

public class ProfileViewModel
{
    public IReadOnlyList<AddressViewModel> Addresses { get; set; } = Array.Empty<AddressViewModel>();
}
