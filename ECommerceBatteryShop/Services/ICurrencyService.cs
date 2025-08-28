namespace ECommerceBatteryShop.Services
{
    public interface ICurrencyService
    {
        Task<decimal?> GetCachedUsdTryAsync(CancellationToken ct = default);
        decimal ConvertUsdToTry(decimal usd, decimal rate);

        // ✅ add this so callers don’t need casts
        Task<decimal?> RefreshNowAsync(CancellationToken ct = default);
    }
}
