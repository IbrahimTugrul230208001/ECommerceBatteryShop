namespace ECommerceBatteryShop.Options
{
    public sealed class CurrencyOptions
    {
        public string BaseUrl { get; set; } = "https://api.collectapi.com";
        public string ApiKey { get; set; } = ""; // "apikey YOUR_TOKEN"
        public int CacheSeconds { get; set; } = 86400; // 24h; we'll refresh 2×/day anyway
        public string[] RefreshTimesLocal { get; set; } = new[] { "12:00", "00:00" }; // TR time
        public decimal FallbackUsdTryRate { get; set; } = 46m; // used when API is unreachable & nothing is cached
    }
}
