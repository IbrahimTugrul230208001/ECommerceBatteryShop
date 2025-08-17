using System.Collections.Generic;

namespace ECommerceBatteryShop.Models
{
    public class ProductListViewModel
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public decimal? MinPrice { get; set; }
        public decimal? MaxPrice { get; set; }
        public float? MinRating { get; set; }
    }
}
