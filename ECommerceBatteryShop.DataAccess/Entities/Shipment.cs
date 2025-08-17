namespace ECommerceBatteryShop.DataAccess.Entities;

public class Shipment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public DateTime ShippedDate { get; set; }
    public string TrackingNumber { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
}
