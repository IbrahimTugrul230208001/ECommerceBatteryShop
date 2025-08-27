// Services/FxThreeTimesDailyRefresher.cs
using ECommerceBatteryShop.Options;
using Microsoft.Extensions.Options;

namespace ECommerceBatteryShop.Services;

public sealed class FxThreeTimesDailyRefresher : BackgroundService
{
    private readonly IServiceProvider _sp;
    private readonly IOptionsMonitor<CurrencyOptions> _opt;
    private readonly ILogger<FxThreeTimesDailyRefresher> _log;
    private readonly TimeZoneInfo _tz = TimeZoneInfo.FindSystemTimeZoneById("Europe/Istanbul");

    public FxThreeTimesDailyRefresher(IServiceProvider sp, IOptionsMonitor<CurrencyOptions> opt, ILogger<FxThreeTimesDailyRefresher> log)
    { _sp = sp; _opt = opt; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var delay = NextDelay();
            _log.LogInformation("FX refresh scheduled in {Delay}", delay);
            try { await Task.Delay(delay, ct); } catch (TaskCanceledException) { break; }

            try
            {
                using var scope = _sp.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<ICurrencyService>() as CurrencyService;
                var rate = await svc!.RefreshNowAsync(ct);
                _log.LogInformation("FX refreshed. USD→TRY = {Rate}", rate);
            }
            catch (Exception ex) { _log.LogWarning(ex, "FX refresh tick failed"); }
        }
    }

    private TimeSpan NextDelay()
    {
        var now = TimeZoneInfo.ConvertTime(DateTimeOffset.UtcNow, _tz).DateTime;
        var times = _opt.CurrentValue.RefreshTimesLocal
            .Select(t => TimeSpan.Parse(t))
            .OrderBy(t => t)
            .ToArray();

        foreach (var t in times)
        {
            var next = now.Date + t;
            if (next > now) return next - now;
        }
        // next day first slot
        return (now.Date.AddDays(1) + times[0]) - now;
    }
}
