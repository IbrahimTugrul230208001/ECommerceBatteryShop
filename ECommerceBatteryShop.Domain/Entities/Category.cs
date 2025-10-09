namespace ECommerceBatteryShop.Domain.Entities;

public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;

    public string Depth { get; set; } = string.Empty;
    public string Slug  { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public ICollection<Category> SubCategories { get; set; } = new List<Category>();
    public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
}
