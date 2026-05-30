namespace ECommerceBatteryShop.Models
{
    public class ProductSectionViewModel
    {
        public string Title { get; set; } = "";
        public string AllLink { get; set; } = "";
        public IEnumerable<ProductViewModel> Products { get; set; } = new List<ProductViewModel>();
        /// <summary>
        /// When true, product card images in this section use 50% resolution thumbnails
        /// from /img/Products/thumbs/ instead of the full-size originals.
        /// </summary>
        public bool UseThumbnails { get; set; }
    }
}
