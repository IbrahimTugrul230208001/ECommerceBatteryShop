using ECommerceBatteryShop.DataAccess.Entities;

namespace ECommerceBatteryShop.Services;

/// <summary>
/// Result of pricing a single product unit for display.
/// <see cref="Original"/> is only populated when a discount applies.
/// </summary>
public readonly record struct PricedUnit(decimal Final, decimal? Original, decimal DiscountPercentage)
{
    public bool HasDiscount => DiscountPercentage > 0m && Original is not null;
}

public interface IPricingService
{
    /// <summary>KDV (VAT) rate applied to every price. 0.20 = 20%.</summary>
    decimal KdvRate { get; }

    /// <summary>
    /// Canonical customer-facing unit price: (ConvertUsdToTry(usdPrice) + extraAmount) * (1 + KDV),
    /// with the best active discount applied. Use this for every product/cart/favorite display.
    /// </summary>
    PricedUnit PriceUnit(decimal usdPrice, int extraAmount, IEnumerable<ProductDiscount>? discounts,
        decimal fxRate, DateTime? asOfUtc = null);

    /// <summary>Convenience overload that reads Price/ExtraAmount/Discounts from a Product entity.</summary>
    PricedUnit PriceUnit(Product product, decimal fxRate, DateTime? asOfUtc = null);

    /// <summary>
    /// Payment/charge line total: the canonical discounted unit price (<see cref="PriceUnit(decimal,int,IEnumerable{ProductDiscount},decimal,DateTime?)"/>)
    /// times quantity, rounded to 2dp away-from-zero. By construction this equals what the cart
    /// displays, so the customer is charged exactly what they saw — discounts included.
    /// </summary>
    decimal ChargeLineTotal(decimal usdUnitPrice, int extraAmount, IEnumerable<ProductDiscount>? discounts,
        int quantity, decimal fxRate);
}

public sealed class PricingService : IPricingService
{
    private readonly ICurrencyService _currency;

    public decimal KdvRate => 0.20m;

    public PricingService(ICurrencyService currency)
    {
        _currency = currency;
    }

    public PricedUnit PriceUnit(Product product, decimal fxRate, DateTime? asOfUtc = null)
        => PriceUnit(product.Price, product.ExtraAmount, product.Discounts, fxRate, asOfUtc);

    public PricedUnit PriceUnit(decimal usdPrice, int extraAmount, IEnumerable<ProductDiscount>? discounts,
        decimal fxRate, DateTime? asOfUtc = null)
    {
        var basePrice = (_currency.ConvertUsdToTry(usdPrice, fxRate) + extraAmount) * (1 + KdvRate);

        var discountPct = ActiveDiscountPercentage(discounts, asOfUtc ?? DateTime.UtcNow);
        if (discountPct > 0m)
        {
            var final = decimal.Round(basePrice * (1 - discountPct / 100m), 2, MidpointRounding.AwayFromZero);
            return new PricedUnit(final, basePrice, discountPct);
        }

        return new PricedUnit(basePrice, null, 0m);
    }

    public decimal ChargeLineTotal(decimal usdUnitPrice, int extraAmount, IEnumerable<ProductDiscount>? discounts,
        int quantity, decimal fxRate)
    {
        var unitPrice = PriceUnit(usdUnitPrice, extraAmount, discounts, fxRate).Final;
        return decimal.Round(unitPrice * quantity, 2, MidpointRounding.AwayFromZero);
    }

    private static decimal ActiveDiscountPercentage(IEnumerable<ProductDiscount>? discounts, DateTime nowUtc)
        => discounts?
            .Where(d => d.IsActive && d.StartDate <= nowUtc && d.EndDate >= nowUtc)
            .OrderByDescending(d => d.DiscountPercentage)
            .FirstOrDefault()?.DiscountPercentage ?? 0m;
}
