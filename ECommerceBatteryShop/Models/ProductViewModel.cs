using System.Net.Mail;

namespace ECommerceBatteryShop.Models
{
    public class ProductViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Price { get; set; }
        public int ExtraAmount { get; set; }
        public string ImageUrl { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public float Rating { get; set; }
        public bool IsFavorite { get; set; }

        // Add this property to fix CS1061
        public string? AttachmentUrl { get; set; }

    }
}
