using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Models
{
    public class OrderViewModel
    {


        public int UserId { get; set; }
        public User? User { get; set; }
        public string? FullAddress { get; set; }
        public string? Status { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
        public List<OrderItem> Items { get; set; } = new List<OrderItem>();
    }
}
