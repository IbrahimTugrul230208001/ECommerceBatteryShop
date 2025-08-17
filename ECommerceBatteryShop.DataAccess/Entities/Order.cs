namespace ECommerceBatteryShop.DataAccess.Entities;

public class Order
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int AddressId { get; set; }
    public int StatusId { get; set; }
    public DateTime OrderDate { get; set; }
    public decimal TotalAmount { get; set; }
}
