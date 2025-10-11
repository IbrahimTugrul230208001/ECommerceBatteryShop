using System;
using Microsoft.Extensions.Caching.Memory;
using System.Threading;

namespace ECommerceBatteryShop.Services;

public interface IThreeDSStore
{
    void SaveInitHtml(string conversationId, string html, TimeSpan? ttl = null);
    bool TryGetInitHtml(string conversationId, out string? html);

    void SaveContext(string conversationId, PendingThreeDSContext context, TimeSpan? ttl = null);
    bool TryGetContext(string conversationId, out PendingThreeDSContext? context);

    void SaveResult(string conversationId, ThreeDSResult result, TimeSpan? ttl = null);
    bool TryGetResult(string conversationId, out ThreeDSResult? result);
}

public class ThreeDSStore : IThreeDSStore
{
    private readonly IMemoryCache _cache;
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromMinutes(20);

    public ThreeDSStore(IMemoryCache cache)
    {
        _cache = cache;
    }

    public void SaveInitHtml(string conversationId, string html, TimeSpan? ttl = null)
        => _cache.Set(GetKey("html", conversationId), html, ttl ?? DefaultTtl);

    public bool TryGetInitHtml(string conversationId, out string? html)
        => _cache.TryGetValue(GetKey("html", conversationId), out html);

    public void SaveContext(string conversationId, PendingThreeDSContext context, TimeSpan? ttl = null)
        => _cache.Set(GetKey("ctx", conversationId), context, ttl ?? DefaultTtl);

    public bool TryGetContext(string conversationId, out PendingThreeDSContext? context)
        => _cache.TryGetValue(GetKey("ctx", conversationId), out context);

    public void SaveResult(string conversationId, ThreeDSResult result, TimeSpan? ttl = null)
        => _cache.Set(GetKey("res", conversationId), result, ttl ?? DefaultTtl);

    public bool TryGetResult(string conversationId, out ThreeDSResult? result)
        => _cache.TryGetValue(GetKey("res", conversationId), out result);

    private static string GetKey(string kind, string id) => $"3ds:{kind}:{id}";
}

public record PendingThreeDSContext(
    int? UserId,
    string? AnonId,
    string? ShippingId,
    decimal ShippingPrice,
    bool SaveCard,
    bool UsedSavedCard,
    ECommerceBatteryShop.Models.GuestCheckoutViewModel? GuestInfo,
    string? CardHolderName
);

public record ThreeDSResult(bool Success, string? RawResponse, string? Message);
