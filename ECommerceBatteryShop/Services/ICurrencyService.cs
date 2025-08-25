namespace ECommerceBatteryShop.Services
{
    public interface ICurrencyService
    {
        /// <summary>Returns current USD→TRY rate.</summary>
        Task<decimal> GetUsdTryAsync(CancellationToken ct = default);

        /// <summary>Converts a USD amount to TRY using latest rate.</summary>
        Task<decimal> ConvertUsdToTryAsync(decimal usdAmount, CancellationToken ct = default);
    }
}
