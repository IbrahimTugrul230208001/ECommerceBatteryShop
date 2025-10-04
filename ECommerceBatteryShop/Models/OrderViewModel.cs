using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Models
{
    public class OrderViewModel
    {
       public List<Order> Orders { get; set; } = new List<Order>();
        public List<OrderItemViewModel> Items { get; set; } = new List<OrderItemViewModel>();
        public List<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();
    }
    public class OrderItemViewModel
    {
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

    }
}
