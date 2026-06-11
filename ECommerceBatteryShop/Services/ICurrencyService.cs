namespace ECommerceBatteryShop.Services
{
    public interface ICurrencyService
    {
        /// <summary>Configured fallback rate when API is unreachable (from appsettings Currency:FallbackUsdTryRate).</summary>
        decimal FallbackRate { get; }

        Task<decimal?> GetCachedUsdTryAsync(CancellationToken ct = default);
        decimal ConvertUsdToTry(decimal usd, decimal rate);

        // ✅ add this so callers don’t need casts
        Task<decimal?> RefreshNowAsync(CancellationToken ct = default);
    }
}
