namespace ECommerceBatteryShop.Domain.Entities;

public class Inventory
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public Product? Product { get; set; }
    public int QuantityInStock { get; set; }
    public DateTime LastUpdated { get; set; }
}
