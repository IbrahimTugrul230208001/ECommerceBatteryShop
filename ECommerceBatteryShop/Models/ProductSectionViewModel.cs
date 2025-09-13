namespace ECommerceBatteryShop.Models
{
    public class ProductSectionViewModel
    {
        public string Title { get; set; } = "";
        public string AllLink { get; set; } = "";
        public IEnumerable<ProductViewModel> Products { get; set; } = new List<ProductViewModel>();
    }
}
