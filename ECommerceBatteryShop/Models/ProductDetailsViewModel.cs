namespace ECommerceBatteryShop.Models
{
    public class ProductDetailsViewModel
    {
        public ProductViewModel product { get; set; } = new ProductViewModel();
        public List<ProductViewModel> RelatedProducts { get; set; } = new List<ProductViewModel>();
    }
}
