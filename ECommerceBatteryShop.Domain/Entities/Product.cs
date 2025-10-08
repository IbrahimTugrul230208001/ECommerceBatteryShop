namespace ECommerceBatteryShop.Domain.Entities;

public class Product
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public decimal Price { get; set; }
    public float Rating { get; set; }
    public int ExtraAmount { get; set; }
    public string? ImageUrl { get; set; } = string.Empty;
    public string? DocumentUrl { get; set; } = string.Empty;
    public Inventory? Inventory { get; set; }
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
}
