namespace ECommerceBatteryShop.Options
{
    public sealed class CurrencyOptions
    {
        public string BaseUrl { get; set; } = "https://api.collectapi.com";
        public string ApiKey { get; set; } = ""; // "apikey YOUR_TOKEN"
        public int CacheSeconds { get; set; } = 86400; // 24h; we'll refresh 3×/day anyway
        public string[] RefreshTimesLocal { get; set; } = new[] { "09:00", "15:00", "21:00" }; // TR time
    }
}
