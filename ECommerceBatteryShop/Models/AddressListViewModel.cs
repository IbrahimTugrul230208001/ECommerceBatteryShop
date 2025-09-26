namespace ECommerceBatteryShop.Models
{
    public class AddressListViewModel
    {
        public string ContainerId { get; set; } = "address-list";
        public IReadOnlyList<AddressViewModel> Addresses { get; set; } = Array.Empty<AddressViewModel>();
    }
}
