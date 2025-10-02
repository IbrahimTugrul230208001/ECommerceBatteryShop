using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Models
{
    public class OrderViewModel
    {
        public int Id { get; set; }
        public int OrderId { get; set; }

        public int UserId { get; set; }
        public User? User { get; set; }
        public Address? Address { get; set; }
        public string? Status { get; set; }
        public DateTime OrderDate { get; set; }
        public decimal TotalAmount { get; set; }
    }
}
