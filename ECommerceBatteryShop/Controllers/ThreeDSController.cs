using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Security.Cryptography;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers;

[AllowAnonymous]
[IgnoreAntiforgeryToken]
public class ThreeDSController : Controller
{
    private const decimal KdvRate = 0.20m;
    private readonly IIyzicoPaymentService _iyzico;
    private readonly IThreeDSStore _store;
    private readonly ICartService _cartService;
    private readonly IOrderRepository _orderRepository;
    private readonly ICurrencyService _currencyService;
    private readonly ISavedCardRepository _savedCardRepository;
    private readonly ILogger<ThreeDSController> _logger;

    public ThreeDSController(
        IIyzicoPaymentService iyzico,
        IThreeDSStore store,
        ICartService cartService,
        IOrderRepository orderRepository,
        ICurrencyService currencyService,
        ISavedCardRepository savedCardRepository,
        ILogger<ThreeDSController> logger)
    {
        _iyzico = iyzico;
        _store = store;
        _cartService = cartService;
        _orderRepository = orderRepository;
        _currencyService = currencyService;
        _savedCardRepository = savedCardRepository;
        _logger = logger;
    }

    [HttpPost("/3ds/callback")]
    public async Task<IActionResult> Callback(CancellationToken ct)
    {
        var form = await Request.ReadFormAsync(ct);
        var conversationId = form["conversationId"].FirstOrDefault() ?? form["ConversationId"].FirstOrDefault();
        var conversationData = form["conversationData"].FirstOrDefault() ?? form["ConversationData"].FirstOrDefault();
        var paymentId = form["paymentId"].FirstOrDefault() ?? form["PaymentId"].FirstOrDefault();
        var mdStatus = form["mdStatus"].FirstOrDefault() ?? form["MdStatus"].FirstOrDefault();

        if (string.IsNullOrWhiteSpace(conversationId) || string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(conversationData))
        {
            _logger.LogWarning("3DS callback missing fields. mdStatus={MdStatus}", mdStatus);
            return View("ThreeDSResult", new ThreeDSViewModel(false, null, "Eksik 3DS bilgileri."));
        }

        var result = await _iyzico.CompleteThreeDSAsync(paymentId, conversationData, conversationId, ct);

        // Build order from stored context
        if (!_store.TryGetContext(conversationId, out var ctx) || ctx is null)
        {
            _logger.LogWarning("3DS context not found for conversationId {ConversationId}", conversationId);
            return View("ThreeDSResult", new ThreeDSViewModel(false, result.RawResponse, result.ErrorMessage ?? "3DS oturumu bulunamadý."));
        }

        try
        {
            var owner = ctx.UserId is int uid ? CartOwner.FromUser(uid) : CartOwner.FromAnon(ctx.AnonId!);
            var cart = await _cartService.GetAsync(owner, createIfMissing: false, ct);
            if (cart is null || cart.Items.Count == 0)
            {
                _logger.LogWarning("Cart missing during 3DS completion. ConversationId={ConversationId}", conversationId);
                return View("ThreeDSResult", new ThreeDSViewModel(false, result.RawResponse, "Sepet bulunamadý."));
            }

            var fx = await _currencyService.GetCachedUsdTryAsync(ct) ?? 30m; // fallback
            decimal basketTotal = 0m;
            foreach (var item in cart.Items)
            {
                var unitPriceTry = item.UnitPrice * (1 + KdvRate) * fx;
                var linePrice = decimal.Round((decimal)(unitPriceTry * item.Quantity), 2, MidpointRounding.AwayFromZero);
                basketTotal += linePrice;
            }
            var paidPrice = basketTotal + ctx.ShippingPrice;

            if (!result.Success)
            {
                _logger.LogWarning("3DS completion returned failure. ConversationId={ConversationId}", conversationId);
                return View("ThreeDSResult", new ThreeDSViewModel(false, result.RawResponse, result.ErrorMessage ?? "3D Secure doðrulamasý baþarýsýz oldu."));
            }

            // Optionally persist saved card if requested and not using a saved card
            if (ctx.SaveCard && !ctx.UsedSavedCard && ctx.UserId is int saveUserId)
            {
                try
                {
                    using var doc = JsonDocument.Parse(result.RawResponse ?? "{}");
                    var root = doc.RootElement;
                    var cardUserKey = root.TryGetProperty("cardUserKey", out var cuk) ? cuk.GetString() : null;
                    if (cardUserKey is null && root.TryGetProperty("CardUserKey", out var cuk2)) cardUserKey = cuk2.GetString();
                    var cardToken = root.TryGetProperty("cardToken", out var ctok) ? ctok.GetString() : null;
                    if (cardToken is null && root.TryGetProperty("CardToken", out var ctok2)) cardToken = ctok2.GetString();
                    var cardAssociation = root.TryGetProperty("cardAssociation", out var assoc) ? assoc.GetString() : null;
                    if (cardAssociation is null && root.TryGetProperty("CardAssociation", out var assoc2)) cardAssociation = assoc2.GetString();
                    var last4 = root.TryGetProperty("lastFourDigits", out var l4) ? l4.GetString() : null;
                    if (last4 is null && root.TryGetProperty("LastFourDigits", out var l42)) last4 = l42.GetString();

                    if (!string.IsNullOrWhiteSpace(cardUserKey) && !string.IsNullOrWhiteSpace(cardToken))
                    {
                        await _savedCardRepository.AddAsync(new SavedCard
                        {
                            UserId = saveUserId,
                            CardUserKey = cardUserKey!,
                            CardToken = cardToken!,
                            Brand = cardAssociation ?? "",
                            Last4 = last4 ?? "",
                            Holder = ""
                        }, ct);
                    }
                }
                catch { }
            }

            // Create order
            var transactionId = ExtractTransactionId(result.RawResponse, conversationId);
            var order = new Order
            {
                OrderId = RandomNumberGenerator.GetInt32(100000, 1_000_000),
                UserId = ctx.UserId,
                AddressId = null,
                AnonId = ctx.UserId is null ? ctx.AnonId : null,
                BuyerName = ctx.GuestInfo is null ? null : $"{ctx.GuestInfo.Name} {ctx.GuestInfo.Surname}".Trim(),
                BuyerEmail = ctx.GuestInfo?.Email,
                BuyerPhone = ctx.GuestInfo?.Phone,
                ShippingAddressText = ctx.GuestInfo?.FullAddress,
                ShippingNeighbourhood = ctx.GuestInfo?.Neighbourhood,
                ShippingState = ctx.GuestInfo?.State,
                ShippingCity = ctx.GuestInfo?.City,
                Status = "Ödeme alýndý",
                OrderDate = DateTime.UtcNow,
                TotalAmount = paidPrice,
                Items = cart.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    Quantity = i.Quantity,
                    UnitPrice = i.UnitPrice
                }).ToList(),
                Payments =
                {
                    new PaymentTransaction
                    {
                        Amount = paidPrice,
                        TransactionDate = DateTime.UtcNow,
                        PaymentMethod = "iyzico",
                        TransactionId = transactionId
                    }
                },
                Shipment = new Shipment
                {
                    Carrier = ResolveCarrier(ctx.ShippingId),
                    TrackingNumber = string.Empty,
                    ShippedDate = DateTime.UtcNow
                }
            };

            await _orderRepository.InsertOrderAsync(order, ct);
            await _cartService.RemoveAllAsync(owner, ct);

            _logger.LogInformation("3DS payment + order success. ConversationId={ConversationId} OrderId={OrderId}", conversationId, order.OrderId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error while processing 3DS callback. ConversationId={ConversationId}", conversationId);
            return View("ThreeDSResult", new ThreeDSViewModel(false, result.RawResponse, "Sipariþ oluþturulurken bir hata oluþtu."));
        }

        return View("ThreeDSResult", new ThreeDSViewModel(true, result.RawResponse, null));
    }

    private static string ResolveCarrier(string? shippingId)
    {
        return (shippingId ?? string.Empty).ToLowerInvariant() switch
        {
            "aras" => "Aras Kargo",
            "hepsijet" => "HepsiJET",
            "yurtici" => "Yurtiçi Kargo",
            _ => "Bilinmeyen Kargo"
        };
    }

    private static string ExtractTransactionId(string? rawResponse, string fallback)
    {
        if (!string.IsNullOrWhiteSpace(rawResponse))
        {
            try
            {
                using var document = JsonDocument.Parse(rawResponse);
                var root = document.RootElement;
                if (root.TryGetProperty("paymentId", out var paymentId))
                {
                    return paymentId.ValueKind switch
                    {
                        JsonValueKind.String => paymentId.GetString() ?? fallback,
                        JsonValueKind.Number => paymentId.ToString() ?? fallback,
                        _ => fallback
                    };
                }
                if (root.TryGetProperty("PaymentId", out var paymentId2))
                {
                    return paymentId2.ValueKind switch
                    {
                        JsonValueKind.String => paymentId2.GetString() ?? fallback,
                        JsonValueKind.Number => paymentId2.ToString() ?? fallback,
                        _ => fallback
                    };
                }

                if (root.TryGetProperty("conversationId", out var conversationId)
                    && conversationId.ValueKind == JsonValueKind.String)
                {
                    return conversationId.GetString() ?? fallback;
                }
                if (root.TryGetProperty("ConversationId", out var conversationId2)
                    && conversationId2.ValueKind == JsonValueKind.String)
                {
                    return conversationId2.GetString() ?? fallback;
                }
            }
            catch (JsonException)
            {
                // ignored - fall back to supplied value
            }
        }

        return fallback;
    }
}

public record ThreeDSViewModel(bool Success, string? RawResponse, string? ErrorMessage);
