// Services/CurrencyService.cs
using System.Text.Json;
using ECommerceBatteryShop.Options;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace ECommerceBatteryShop.Services;

public sealed class CurrencyService : ICurrencyService
{
    private readonly HttpClient _http;
    private readonly IMemoryCache _cache;
    private readonly CurrencyOptions _opt;
    private readonly ILogger<CurrencyService> _log;

    private const string CacheKeyRate = "USDTRY_RATE";
    private const string CacheKeyLkg = "USDTRY_LKG";
    private const decimal HardFallbackRate = 41m; // <- your fixed multiplier
    public CurrencyService(HttpClient http, IMemoryCache cache, IOptions<CurrencyOptions> opt, ILogger<CurrencyService> log)
    {
        _http = http; _cache = cache; _opt = opt.Value; _log = log;
        _http.BaseAddress = new Uri(_opt.BaseUrl);
        _http.DefaultRequestHeaders.TryAddWithoutValidation("authorization", _opt.ApiKey); // "apikey <token>"
        _http.DefaultRequestHeaders.TryAddWithoutValidation("content-type", "application/json");
    }

    public async Task<decimal?> GetCachedUsdTryAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKeyRate, out decimal r)) return r;
        if (_cache.TryGetValue(CacheKeyLkg, out decimal lkg)) return lkg; // allow immediate use after startup
        return HardFallbackRate; // use hard fallback immediately if nothing cached
    }

    public decimal ConvertUsdToTry(decimal usd, decimal rate)
        => Math.Round(usd * rate, 2, MidpointRounding.AwayFromZero);

    // Called by the refresher; you can call manually too
    public async Task<decimal?> RefreshNowAsync(CancellationToken ct = default)
    {
        try
        {
            // GET /economy/currencyToAll?int=10&base=USD
            using var resp = await _http.GetAsync("/economy/currencyToAll?int=10&base=USD", ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("CollectAPI status: {Status}", resp.StatusCode);
                return UseLkgOrNull();
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            if (TryExtractUsdTry(json, out var rate) && rate > 0)
            {
                Cache(rate);
                return rate;
            }

            _log.LogWarning("CollectAPI parse failed. Head: {Head}", json[..Math.Min(json.Length, 300)]);
            return UseLkgOrNull();
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "CollectAPI refresh failed");
            return UseLkgOrNull();
        }
    }

    private decimal? UseLkgOrNull()
        => _cache.TryGetValue(CacheKeyLkg, out decimal lkg) ? lkg : (decimal?)null;

    private void Cache(decimal rate)
    {
        _cache.Set(CacheKeyRate, rate, TimeSpan.FromSeconds(Math.Max(60, _opt.CacheSeconds)));
        _cache.Set(CacheKeyLkg, rate); // no expiration
    }

    // Lenient extractor; adjust if you know the exact schema.
    private static bool TryExtractUsdTry(string raw, out decimal rate)
    {
        rate = 0m;
        using var doc = JsonDocument.Parse(raw);
        // common shapes: { "success":true, "result":[{ "code":"TRY", "rate": 34.1, ...}, ...] }
        if (doc.RootElement.TryGetProperty("result", out var result) && result.ValueKind == JsonValueKind.Array)
        {
            foreach (var el in result.EnumerateArray())
            {
                if (!el.TryGetProperty("code", out var codeEl)) continue;
                var code = codeEl.GetString();
                if (!string.Equals(code, "TRY", StringComparison.OrdinalIgnoreCase)) continue;

                if (el.TryGetProperty("rate", out var rateEl))
                {
                    if (rateEl.ValueKind == JsonValueKind.Number)
                        rate = rateEl.TryGetDecimal(out var d) ? d : (decimal)rateEl.GetDouble();
                    else if (rateEl.ValueKind == JsonValueKind.String && decimal.TryParse(rateEl.GetString(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var ds))
                        rate = ds;
                    return rate > 0;
                }
            }
        }
        return false;
    }
}
