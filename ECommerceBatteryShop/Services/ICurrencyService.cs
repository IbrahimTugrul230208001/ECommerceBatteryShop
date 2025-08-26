namespace ECommerceBatteryShop.Services
{
    public interface ICurrencyService
    {
        /// <summary>Returns USD→TRY or null if unavailable (uses LKG if present).</summary>
        Task<decimal?> TryGetUsdTryAsync(CancellationToken ct = default);

        /// <summary>Pure helper (no I/O).</summary>
        decimal ConvertUsdToTry(decimal usd, decimal usdTryRate);

    }
}