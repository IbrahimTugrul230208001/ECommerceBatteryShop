using ECommerceBatteryShop.Options;
using Microsoft.Extensions.Options;

namespace ECommerceBatteryShop.Services;

public sealed class FxThreeTimesDailyRefresher : BackgroundService
{
    private readonly ICurrencyService _svc;
    private readonly IOptionsMonitor<CurrencyOptions> _opt;
    private readonly ILogger<FxThreeTimesDailyRefresher> _log;
    private readonly TimeZoneInfo _tz;

    public FxThreeTimesDailyRefresher(
        ICurrencyService svc,                   // ✅ inject the service directly
        IOptionsMonitor<CurrencyOptions> opt,
        ILogger<FxThreeTimesDailyRefresher> log)
    {
        _svc = svc; _opt = opt; _log = log;
        _tz = TryTz("Europe/Istanbul") ?? TryTz("Turkey Standard Time") ?? TimeZoneInfo.Local;
        static TimeZoneInfo? TryTz(string id) { try { return TimeZoneInfo.FindSystemTimeZoneById(id); } catch { return null; } }
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        // run once immediately so you see Console/Log output
        //await TryRun(ct);

        while (!ct.IsCancellationRequested)
        {
            var delay = NextDelay();
            _log.LogInformation("FX refresh scheduled in {Delay}", delay);
            try { await Task.Delay(delay, ct); } catch (TaskCanceledException) { break; }
            await TryRun(ct);
        }
    }

    private async Task TryRun(CancellationToken ct)
    {
        try
        {
            var rate = await _svc.RefreshNowAsync(ct);
            Console.WriteLine($"[FxRefresher] USD→TRY = {rate}");
            _log.LogInformation("FX refreshed. USD→TRY = {Rate}", rate);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "FX refresh tick failed");
        }
    }

    private TimeSpan NextDelay()
    {
        var nowLocal = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _tz).DateTime;
        var times = (_opt.CurrentValue.RefreshTimesLocal ?? Array.Empty<string>())
            .Select(t => TimeSpan.TryParse(t, out var ts) ? ts : (TimeSpan?)null)
            .Where(ts => ts.HasValue).Select(ts => ts!.Value)
            .OrderBy(ts => ts)
            .ToArray();

        if (times.Length == 0) times = new[] { new TimeSpan(9, 0, 0), new TimeSpan(15, 0, 0), new TimeSpan(21, 0, 0) };

        foreach (var t in times)
        {
            var next = nowLocal.Date + t;
            if (next > nowLocal) return next - nowLocal;
        }
        return (nowLocal.Date.AddDays(1) + times[0]) - nowLocal;
    }
}
