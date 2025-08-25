using ECommerceBatteryShop.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Text.Json;
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

        private const string CacheKeyUsdTry = "USDTRY_RATE";

        public CurrencyService(HttpClient http, IMemoryCache cache, IOptions<CurrencyOptions> opt)
        {
            _http = http;
            _cache = cache;
            _opt = opt.Value;

            _http.BaseAddress = new Uri(_opt.BaseUrl); // e.g. https://api.exchangerate.host/
        }

        public async Task<decimal> GetUsdTryAsync(CancellationToken ct = default)
        {
            if (_cache.TryGetValue(CacheKeyUsdTry, out decimal cached))
                return cached;

            // GET https://api.exchangerate.host/latest?base=USD&symbols=TRY
            using var resp = await _http.GetAsync("latest?base=USD&symbols=TRY", ct);
            resp.EnsureSuccessStatusCode();

            var json = await resp.Content.ReadAsStringAsync(ct);
            var dto = System.Text.Json.JsonSerializer.Deserialize<ExchangerateHostModels.LatestDto>(json, JsonOpts)
                      ?? throw new InvalidOperationException("Empty JSON");

            if (dto.Rates is null || !dto.Rates.TryGetValue("TRY", out var rate))
                throw new InvalidOperationException("TRY rate missing");

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
