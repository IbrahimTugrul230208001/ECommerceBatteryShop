using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Models
{
    public class OrderViewModel
    {
       public List<Order> Orders { get; set; } = new List<Order>();
        public List<PaymentTransaction> Payments { get; set; } = new List<PaymentTransaction>();
        public decimal Rate { get; set; }
    }
    public class OrderItemViewModel
    {
        public int? OrderId { get; set; }
        public string? ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal UnitPrice { get; set; }

    }
}
