namespace ECommerceBatteryShop.Models
{
    public class FavoriteItemViewModel
    {
        public int ProductId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? ImageUrl { get; set; }
        public decimal UnitPrice { get; set; }
        public int Quantity { get; set; }
        public decimal LineTotal => UnitPrice * Quantity;
    }

    public class FavoriteViewModel
    {
        public List<FavoriteItemViewModel> Items { get; set; } = new();
        public decimal SubTotal => Items.Sum(i => i.LineTotal);
        public bool CookiesDisabled { get; set; }
        public string? CookieMessage { get; set; }
    }
}
