using System.Collections.Generic;

namespace ECommerceBatteryShop.Models
{
    public sealed class ProductSalesAnalyticsViewModel
    {
        public string? Search { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 30;
        public int TotalCount { get; set; }
        public List<ProductSalesAnalyticsItem> Items { get; set; } = new();
    }

    public sealed class ProductSalesAnalyticsItem
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal Price { get; set; }
        public int SoldUnits { get; set; }
    }
}
