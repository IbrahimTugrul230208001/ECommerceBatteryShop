namespace ECommerceBatteryShop.Services
{
    public interface ICurrencyService
    {
        Task<decimal?> GetCachedUsdTryAsync(CancellationToken ct = default); // null => not available
        decimal ConvertUsdToTry(decimal usd, decimal rate);

    }
}