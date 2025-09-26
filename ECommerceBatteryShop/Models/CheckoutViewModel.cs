namespace ECommerceBatteryShop.Models
{
    public class CheckoutViewModel
    {
        public decimal Subtotal { get; set; }

        public AddressListViewModel AddressList { get; set; } = new AddressListViewModel();

        public decimal Tax => Subtotal * 0.2m;

        public decimal Shipping => 150m;

        public decimal Total => Subtotal + Shipping;
    }
}
