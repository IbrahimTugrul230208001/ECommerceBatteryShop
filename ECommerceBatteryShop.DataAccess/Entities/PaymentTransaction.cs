namespace ECommerceBatteryShop.DataAccess.Entities;

public class PaymentTransaction
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public Order? Order { get; set; }
    public decimal Amount { get; set; }
    public DateTime TransactionDate { get; set; }
    public string? PaymentMethod { get; set; }
    public string? TransactionId { get; set; }
}
