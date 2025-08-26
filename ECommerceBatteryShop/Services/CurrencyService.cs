using ECommerceBatteryShop.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text.Json;
using static ECommerceBatteryShop.Services.ExchangerateHostModels;
using static System.Net.WebRequestMethods;

namespace ECommerceBatteryShop.Services
{
    public sealed class CurrencyService : ICurrencyService
    {
        private readonly HttpClient _http;
        private readonly IMemoryCache _cache;
        private readonly CurrencyOptions _opt;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true
        };

        private const string CacheKeyRate = "USDTRY_RATE";
        private const string CacheKeyLkg = "USDTRY_LKG"; // non-expiring last-known-good

        public CurrencyService(HttpClient http, IMemoryCache cache, IOptions<CurrencyOptions> opt)
        {
            _http = http;
            _cache = cache;
            _opt = opt.Value;
            _http.BaseAddress = new Uri(_opt.BaseUrl);
        }
        public async Task<decimal?> TryGetUsdTryAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKeyRate, out decimal cached)) return cached;

            // Single call; avoid relying on base= behavior (EUR is default)
            // GET /latest?symbols=USD,TRY
            for (var attempt = 0; attempt < 2; attempt++) // tiny retry
            {
                try
                {
                    using var resp = await _http.GetAsync("latest?symbols=USD,TRY", ct);
                    if (!resp.IsSuccessStatusCode) continue;

                    var dto = await resp.Content.ReadFromJsonAsync<LatestDto>(JsonOpts, ct);
                    if (dto?.Rates is null) continue;

                    if (!dto.Rates.TryGetValue("TRY", out var tryPerEur)) continue; // TRY/EUR
                    if (!dto.Rates.TryGetValue("USD", out var usdPerEur)) continue; // USD/EUR
                    if (tryPerEur <= 0 || usdPerEur <= 0) continue;

                    var tryPerUsd = tryPerEur / usdPerEur; // (TRY/EUR)/(USD/EUR)
                    if (tryPerUsd <= 0) continue;

                    _cache.Set(CacheKeyRate, tryPerUsd, TimeSpan.FromSeconds(Math.Max(30, _opt.CacheSeconds)));
                    _cache.Set(CacheKeyLkg, tryPerUsd); // refresh LKG
                    return tryPerUsd;
                }
                catch { /* swallow transient; try again once */ }
            }

            // Fallback to LKG if we have it
            if (_cache.TryGetValue(CacheKeyLkg, out decimal lkg)) return lkg;

            return null; // nothing available
        }

        public decimal ConvertUsdToTry(decimal usd, decimal usdTryRate)
            => Math.Round(usd * usdTryRate, 2, MidpointRounding.AwayFromZero);
    }
}
