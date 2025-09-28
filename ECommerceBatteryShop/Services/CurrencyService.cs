// Services/CurrencyService.cs
using System.Globalization;
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
    private const decimal HardFallbackRate = 41.5m; // fixed multiplier if nothing cached

    public CurrencyService(HttpClient http, IMemoryCache cache, IOptions<CurrencyOptions> opt, ILogger<CurrencyService> log)
    {
        _http = http; _cache = cache; _opt = opt.Value; _log = log;

        _http.BaseAddress = new Uri(_opt.BaseUrl);

        // Normalize "authorization" header so both "apikey x" and "x" in config work.
        var key = (_opt.ApiKey ?? string.Empty).Trim();
        if (!key.StartsWith("apikey ", StringComparison.OrdinalIgnoreCase))
            key = "apikey " + key;

        _http.DefaultRequestHeaders.Remove("authorization");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("authorization", key);
        _http.DefaultRequestHeaders.Remove("accept");
        _http.DefaultRequestHeaders.TryAddWithoutValidation("accept", "application/json");
    }

    public async Task<decimal?> GetCachedUsdTryAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(CacheKeyRate, out decimal r)) return r;
        if (_cache.TryGetValue(CacheKeyLkg, out decimal lkg)) return lkg; // allow immediate use after startup
        return HardFallbackRate; // immediate hard fallback if nothing cached
    }

    public decimal ConvertUsdToTry(decimal usd, decimal rate)
        => Math.Round(usd * rate, 2, MidpointRounding.AwayFromZero);

    /// <summary>
    /// Tries to refresh USD→TRY from CollectAPI and cache it.
    /// </summary>
    public async Task<decimal?> RefreshNowAsync(CancellationToken ct = default)
    {
        try
        {
            // Your sample payload is from /economy/allCurrency (TRY-based list of many FX).
            using var resp = await _http.GetAsync("/economy/allCurrency", ct);
            if (!resp.IsSuccessStatusCode)
            {
                _log.LogWarning("CollectAPI status: {Status}", resp.StatusCode);
                return UseLkgOrNull();
            }

            var json = await resp.Content.ReadAsStringAsync(ct);
            if (TryExtractUsdTry(json, out var rate) && rate > 0)
            {
                _log.LogInformation("Extracted USD/TRY selling={Rate}", rate);           // better for logging
                Console.WriteLine($"[CurrencyService] Extracted USD/TRY selling={rate}"); // 👈 here

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

    /// <summary>
    /// Extracts USD→TRY from /economy/allCurrency response.
    /// Chooses USD.selling; falls back to calculated → buying → localized strings.
    /// </summary>
    private static bool TryExtractUsdTry(string raw, out decimal rate)
    {
        rate = 0m;
        using var doc = JsonDocument.Parse(raw);

        if (!doc.RootElement.TryGetProperty("result", out var result) ||
            result.ValueKind != JsonValueKind.Array)
            return false;

        foreach (var el in result.EnumerateArray())
        {
            if (!el.TryGetProperty("code", out var codeEl)) continue;
            if (!string.Equals(codeEl.GetString(), "USD", StringComparison.OrdinalIgnoreCase)) continue;

            if (TryGetNumber(el, "selling", out rate) && rate > 0) return true;
            if (TryGetNumber(el, "calculated", out rate) && rate > 0) return true;
            if (TryGetNumber(el, "buying", out rate) && rate > 0) return true;

            if (TryParseTr(el, "sellingstr", out rate) && rate > 0) return true;
            if (TryParseTr(el, "buyingstr", out rate) && rate > 0) return true;

            return false;
        }
        return false;

        static bool TryGetNumber(JsonElement obj, string name, out decimal val)
        {
            val = 0m;
            if (!obj.TryGetProperty(name, out var p)) return false;
            if (p.ValueKind != JsonValueKind.Number) return false;
            if (p.TryGetDecimal(out val)) return true;
            if (p.TryGetDouble(out var d)) { val = (decimal)d; return true; }
            return false;
        }

        // e.g., "47,5885" → decimal using tr-TR
        static bool TryParseTr(JsonElement obj, string name, out decimal val)
        {
            val = 0m;
            if (!obj.TryGetProperty(name, out var p)) return false;
            if (p.ValueKind != JsonValueKind.String) return false;
            var s = p.GetString();
            return decimal.TryParse(s, NumberStyles.Number, new CultureInfo("tr-TR"), out val);
        }
    }
}
