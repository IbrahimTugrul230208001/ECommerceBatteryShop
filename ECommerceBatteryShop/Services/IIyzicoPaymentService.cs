using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Services;

public sealed record IyzicoBuyerInfo
{
    public string? Id { get; init; }
    public string? FirstName { get; init; }
    public string? LastName { get; init; }
    public string? Email { get; init; }
    public string? PhoneNumber { get; init; }
    public string? IdentityNumber { get; init; }
    public string? AddressLine { get; init; }
    public string? City { get; init; }
    public string? Country { get; init; }
    public string? ZipCode { get; init; }
}

public sealed record IyzicoCheckoutContext
{
    public Cart Cart { get; init; } = null!;
    public decimal ItemsTotal { get; init; }
    public decimal ShippingCost { get; init; }
    public decimal FxRate { get; init; }
    public IyzicoBuyerInfo Buyer { get; init; } = new();
    public string? BuyerIpAddress { get; init; }
    public string? BasketId { get; init; }
    public string? ConversationId { get; init; }
    public string? CallbackUrl { get; init; }
}

public interface IIyzicoPaymentService
{
    Task<string?> InitializeCheckoutFormAsync(IyzicoCheckoutContext context, CancellationToken ct);
}
