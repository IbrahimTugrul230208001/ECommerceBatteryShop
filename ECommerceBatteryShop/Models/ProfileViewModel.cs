using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Models;

public class ProfileViewModel
{
    public IReadOnlyList<AddressViewModel> Addresses { get; set; } = Array.Empty<AddressViewModel>();
    public IReadOnlyList<Order> Orders { get; set; } = Array.Empty<Order>();
}
