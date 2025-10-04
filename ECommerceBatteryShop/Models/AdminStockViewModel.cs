using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models
{
    public class AdminStockViewModel
    {
        public IList<AdminStockItemViewModel> Items { get; set; } = new List<AdminStockItemViewModel>();

        public string? SearchTerm { get; set; }
    }

    public class AdminStockItemViewModel
    {
        [Required]
        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        public int Quantity { get; set; }
    }
}
