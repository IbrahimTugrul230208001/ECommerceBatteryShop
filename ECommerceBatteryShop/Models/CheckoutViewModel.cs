namespace ECommerceBatteryShop.Models
{
    public class CheckoutViewModel
    {
            public List<CartItemViewModel> Items { get; set; } = new();
            public decimal SubTotal => Items.Sum(i => i.LineTotal);
            public decimal Tax => SubTotal * 0.2m;
            public decimal Total => SubTotal + Tax;
    }
}
