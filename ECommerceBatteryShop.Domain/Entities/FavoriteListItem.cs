namespace ECommerceBatteryShop.Domain.Entities
{
    public class FavoriteListItem
    {
        public int Id { get; set; }
        public int FavoriteId { get; set; }
        public FavoriteList? Favorite { get; set; }
        public int ProductId { get; set; }
        public Product? Product { get; set; }
    }
}