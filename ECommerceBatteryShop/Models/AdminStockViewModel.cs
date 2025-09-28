using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ECommerceBatteryShop.Models
{
    public class AdminStockViewModel
    {
        public IList<AdminStockItemViewModel> Items { get; set; } = new List<AdminStockItemViewModel>();
    }

    public class AdminStockItemViewModel
    {
        [Required]
        public int ProductId { get; set; }

        public string ProductName { get; set; } = string.Empty;

        [Display(Name = "Stok Durumu")]
        public bool InStock { get; set; }
    }
}
