namespace ECommerceBatteryShop.Options
{
    public sealed class CurrencyOptions
    {
        public string BaseUrl { get; set; } = "https://open.er-api.com/v6/";
        public int CacheSeconds { get; set; } = 600; // 10 min
    }
}
