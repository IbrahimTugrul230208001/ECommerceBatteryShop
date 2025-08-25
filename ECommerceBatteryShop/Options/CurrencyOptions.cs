namespace ECommerceBatteryShop.Options
{
    public sealed class CurrencyOptions
    {
        public string BaseUrl { get; set; } = "https://api.exchangerate.host/";
        public int CacheSeconds { get; set; } = 600; // 10 min
    }
}
