namespace ECommerceBatteryShop.DataAccess.Entities;

public class ProductImage
{
    public int Id { get; set; }
    public int ProductId { get; set; }
    public string Url { get; set; } = string.Empty;
    public string? AltText { get; set; }
    public bool IsPrimary { get; set; }
}
