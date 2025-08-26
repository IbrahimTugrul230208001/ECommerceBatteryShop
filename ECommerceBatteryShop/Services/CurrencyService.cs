using ECommerceBatteryShop.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace ECommerceBatteryShop.Services
{
    public sealed class CurrencyService : ICurrencyService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly CurrencyOptions _opt;

        private const string CacheKeyUsdTry = "USDTRY_RATE";

        public CurrencyService(HttpClient http, IMemoryCache cache, IOptions<CurrencyOptions> opt)
        {
            _http = http;
            _cache = cache;
            _opt = opt.Value;

            _http.BaseAddress = new Uri(_opt.BaseUrl); // e.g. https://open.er-api.com/v6/
        }

        public async Task<decimal> GetUsdTryAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKeyUsdTry, out decimal cached))
                return cached;

            // GET https://open.er-api.com/v6/latest/USD
            using var resp = await _http.GetAsync("latest/USD", ct);
            resp.EnsureSuccessStatusCode();

            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (!doc.RootElement.TryGetProperty("rates", out var rates) ||
                !rates.TryGetProperty("TRY", out var rateEl))
            {
                throw new InvalidOperationException("TRY rate missing");
            }

            var rate = rateEl.GetDecimal();

            _cache.Set(CacheKeyUsdTry, rate, TimeSpan.FromSeconds(Math.Max(30, _opt.CacheSeconds)));
            return rate;
        }

        public async Task<decimal> ConvertUsdToTryAsync(decimal usdAmount, CancellationToken ct = default)
        {
            var rate = await GetUsdTryAsync(ct);
            return Math.Round(usdAmount * rate, 2, MidpointRounding.AwayFromZero);
        }
    }
}

