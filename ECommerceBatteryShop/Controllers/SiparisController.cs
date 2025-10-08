using System.Globalization;
using System.Linq;
using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using System.Text.Json;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class SiparisController : Controller
{
    private const decimal KdvRate = 0.20m;
    private const decimal DefaultShippingFee = 129.99m;
    private const decimal IbanDiscountRate = 0.03m; // %3 indirim

    private readonly ICartService _cartService;
    private readonly ICurrencyService _currencyService;
    private readonly IAddressRepository _addressRepository;
    private readonly IOrderRepository _orderRepository;
    private readonly IIyzicoPaymentService _paymentService;
    private readonly ISavedCardRepository _savedCardRepository;
    private readonly ILogger<SiparisController> _logger;

    private const string GuestInfoCookie = "GUEST_INFO";

    public SiparisController(
        ICartService cartService,
        ICurrencyService currencyService,
        IAddressRepository addressRepository,
        IOrderRepository orderRepository,
        IIyzicoPaymentService paymentService,
        ISavedCardRepository savedCardRepository,
        ILogger<SiparisController> logger)
    {
        _cartService = cartService;
        _currencyService = currencyService;
        _addressRepository = addressRepository;
        _orderRepository = orderRepository;
        _paymentService = paymentService;
        _savedCardRepository = savedCardRepository;
        _logger = logger;
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

    [HttpGet]
    public async Task<IActionResult> SavedCards(CancellationToken ct)
    {
        if (User.Identity?.IsAuthenticated != true)
            return Unauthorized();

        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !int.TryParse(userIdClaim.Value, out var userId))
            return Unauthorized();

        var cards = await _savedCardRepository.GetByUserAsync(userId, ct);
        return Json(cards.Select(c => new { id = c.Id, brand = c.Brand, last4 = c.Last4, holder = c.Holder }));
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> PlaceOrder([FromForm] PlaceOrderInputModel input, CancellationToken cancellationToken)
    {
        var fxRate = await _currencyService.GetCachedUsdTryAsync(cancellationToken);
        var shippingPrice = SanitizeShipping(input.ShippingPrice);
        
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                            ?? "Geçersiz istek.";
            return BadRequest(new { success = false, message = firstError });
        }

        if (!TryResolveOwner(out var owner, out var errorResult))
        {
            return errorResult ?? BadRequest(new { success = false, message = "Sepet bulunamadı." });
        }

        var cart = await _cartService.GetAsync(owner, createIfMissing: false, cancellationToken);
        if (cart is null || cart.Items.Count == 0)
        {
            return BadRequest(new { success = false, message = "Sepetiniz boş olduğu için ödeme alınamadı." });
        }

        var isGuest = owner.UserId is null;

        if (string.Equals(input.PaymentMethod, "iban", StringComparison.OrdinalIgnoreCase))
        {
            if (!isGuest)
            {
                // existing authenticated flow: unchanged
                if (owner.UserId is not int userId1)
                {
                    return BadRequest(new { success = false, message = "IBAN ile ödeme için giriş yapmanız gerekmektedir." });
                }

                var address = await ResolveAddressAsync(userId1, cancellationToken);
                if (address is null)
                {
                    return BadRequest(new { success = false, message = "Sipariş oluşturmak için kayıtlı bir adres bulunamadı." });
                }

                var orderTotalBeforeDiscount = CalculateOrderTotal(cart, (decimal)fxRate) + shippingPrice;
                var discountedTotal = decimal.Round(orderTotalBeforeDiscount * (1 - IbanDiscountRate), 2, MidpointRounding.AwayFromZero);

                var order1 = new Order
                {
                    OrderId = GenerateOrderNumber(),
                    UserId = userId1,
                    AddressId = address.Id,
                    Status = "Sipariş alındı",
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = discountedTotal,
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
                            Amount = discountedTotal,
                            TransactionDate = DateTime.UtcNow,
                            PaymentMethod = "iban",
                            TransactionId = null 
                        }
                    },
                    Shipment = new Shipment
                    {
                        Carrier = ResolveCarrier(input.ShippingId),
                        TrackingNumber = string.Empty,
                        ShippedDate = DateTime.UtcNow
                    }
                };

                await _orderRepository.InsertOrderAsync(order1, cancellationToken);
                await _cartService.RemoveAllAsync(owner, cancellationToken);

                _logger.LogInformation("IBAN order created successfully with discount. OrderNumber: {OrderNumber}, UserId: {UserId}", order1.OrderId, userId1);

                return Ok(new
                {
                    success = true,
                    message = $"Siparişiniz başarıyla alındı. IBAN ile ödeme indirimi (%3) uygulandı. Sipariş numaranız: {order1.OrderId.ToString("D6", CultureInfo.InvariantCulture)}. Sipariş numarasını açıklamaya giriniz."
                });
            }
            else
            {
                // New guest flow for IBAN
                var guest = ReadGuestInfoFromRequest() ?? FromInput(input);
                if (guest is null)
                {
                    return BadRequest(new { success = false, message = "Misafir bilgileri eksik. Lütfen ad, soyad ve adres bilgilerini giriniz." });
                }

                var orderTotalBeforeDiscount = CalculateOrderTotal(cart, (decimal)fxRate) + shippingPrice;
                var discountedTotal = decimal.Round(orderTotalBeforeDiscount * (1 - IbanDiscountRate), 2, MidpointRounding.AwayFromZero);

                var order = new Order
                {
                    OrderId = GenerateOrderNumber(),
                    UserId = null,
                    AddressId = null,
                    AnonId = cart.AnonId,
                    BuyerName = $"{guest.Name} {guest.Surname}".Trim(),
                    BuyerEmail = guest.Email,
                    BuyerPhone = SanitizeDigits(guest.Phone),
                    ShippingAddressText = guest.FullAddress,
                    ShippingNeighbourhood = guest.Neighbourhood,
                    ShippingState = guest.State,
                    ShippingCity = guest.City,
                    Status = "Sipariş alındı",
                    OrderDate = DateTime.UtcNow,
                    TotalAmount = discountedTotal,
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
                            Amount = discountedTotal,
                            TransactionDate = DateTime.UtcNow,
                            PaymentMethod = "iban",
                            TransactionId = null
                        }
                    },
                    Shipment = new Shipment
                    {
                        Carrier = ResolveCarrier(input.ShippingId),
                        TrackingNumber = string.Empty,
                        ShippedDate = DateTime.UtcNow
                    }
                };

                await _orderRepository.InsertOrderAsync(order, cancellationToken);
                await _cartService.RemoveAllAsync(owner, cancellationToken);

                _logger.LogInformation("Guest IBAN order created successfully. OrderNumber: {OrderNumber}, AnonId: {AnonId}", order.OrderId, cart.AnonId);

                return Ok(new
                {
                    success = true,
                    message = $"Siparişiniz başarıyla alındı. IBAN ile ödeme indirimi (%3) uygulandı. Sipariş numaranız: {order.OrderId.ToString("D6", CultureInfo.InvariantCulture)}. Sipariş numarasını açıklamaya giriniz."
                });
            }
        }

        // Handle card payments
        bool useSaved = string.Equals(input.PaymentMethod, "card_saved", StringComparison.OrdinalIgnoreCase);
        if (!useSaved && !string.Equals(input.PaymentMethod, "card_new", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Seçilen ödeme yöntemi şu anda desteklenmiyor." });
        }

        if (!isGuest)
        {
            // existing authenticated card flow: unchanged
            if (owner.UserId is not int userId)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Kart ile ödeme için giriş yapmanız gerekmektedir."
                });
            }

            var orderAddress = await ResolveAddressAsync(userId, cancellationToken);
            if (orderAddress is null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Kartla ödeme için kayıtlı bir adres bulunamadı."
                });
            }

            var lineItems = new List<IyzicoBasketItem>();
            decimal basketTotal = 0m;
            foreach (var item in cart.Items)
            {
                var unitPriceTry = item.UnitPrice * (1 + KdvRate) * fxRate;
                var linePrice = decimal.Round((decimal)(unitPriceTry * item.Quantity), 2, MidpointRounding.AwayFromZero);
                basketTotal += linePrice;
                lineItems.Add(new IyzicoBasketItem
                {
                    Id = item.ProductId.ToString(CultureInfo.InvariantCulture),
                    Name = item.Product?.Name ?? $"Ürün #{item.ProductId}",
                    Price = linePrice
                });
            }

            if (lineItems.Count == 0)
            {
                return BadRequest(new { success = false, message = "Ödeme için sepet ürünü bulunamadı." });
            }

            var paidPrice = basketTotal + shippingPrice;

            var buyerContext = await BuildBuyerContextAsync(owner, cart, cancellationToken);

            var paymentModel = new IyzicoPaymentModel
            {
                ConversationId = Guid.NewGuid().ToString("N"),
                BasketId = cart.Id.ToString(CultureInfo.InvariantCulture),
                Price = basketTotal,
                PaidPrice = paidPrice,
                Buyer = buyerContext.Buyer,
                BillingAddress = buyerContext.Billing,
                ShippingAddress = buyerContext.Shipping,
                Items = lineItems
            };

            if (useSaved)
            {
                if (string.IsNullOrWhiteSpace(input.CardId) || !int.TryParse(input.CardId, out var savedId))
                {
                    return BadRequest(new { success = false, message = "Kayıtlı kart seçimi geçersiz." });
                }
                var saved = (await _savedCardRepository.GetByUserAsync(userId, cancellationToken)).FirstOrDefault(c => c.Id == savedId);
                if (saved is null)
                {
                    return BadRequest(new { success = false, message = "Kayıtlı kart bulunamadı." });
                }
                paymentModel.Saved = new IyzicoSavedCard { CardUserKey = saved.CardUserKey, CardToken = saved.CardToken };
            }
            else
            {
                if (!TryParseCard(input, out var cardInfo, out var cardError))
                {
                    return BadRequest(new { success = false, message = cardError ?? "Kart bilgileri eksik veya hatalı." });
                }

                cardInfo.RegisterCard = input.Save;
                paymentModel.Card = cardInfo;
            }

            var result = await _paymentService.CreatePaymentAsync(paymentModel, cancellationToken);
            if (!result.Success)
            {
                // If card saving is not enabled for merchant (Iyzico error 3007), retry without saving
                if (!useSaved && input.Save && IsCardStorageNotEnabled(result.RawResponse))
                {
                    _logger.LogWarning("Card storage disabled on merchant. Retrying payment without saving card. ConversationId: {ConversationId}", paymentModel.ConversationId);
                    if (paymentModel.Card is not null)
                    {
                        paymentModel.Card.RegisterCard = false;
                        var retry = await _paymentService.CreatePaymentAsync(paymentModel, cancellationToken);
                        if (!retry.Success)
                        {
                            var retryMessage = string.IsNullOrWhiteSpace(retry.ErrorMessage)
                                ? "Ödeme sırasında bir hata oluştu. Lütfen tekrar deneyin."
                                : retry.ErrorMessage;
                            return StatusCode((int)HttpStatusCode.BadGateway, new { success = false, message = retryMessage });
                        }
                        result = retry; // use successful retry result
                    }
                    else
                    {
                        var errorMessageA = string.IsNullOrWhiteSpace(result.ErrorMessage)
                            ? "Ödeme sırasında bir hata oluştu. Lütfen tekrar deneyin."
                            : result.ErrorMessage;
                        return StatusCode((int)HttpStatusCode.BadGateway, new { success = false, message = errorMessageA });
                    }
                }
                else
                {
                    var errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                        ? "Ödeme sırasında bir hata oluştu. Lütfen tekrar deneyin."
                        : result.ErrorMessage;
                    return StatusCode((int)HttpStatusCode.BadGateway, new { success = false, message = errorMessage });
                }
            }

            // Parse payment response for card info when saved
            if (!useSaved && input.Save)
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
                    var holder = input.Name ?? "";
                    if (!string.IsNullOrWhiteSpace(cardUserKey) && !string.IsNullOrWhiteSpace(cardToken))
                    {
                        await _savedCardRepository.AddAsync(new SavedCard
                        {
                            UserId = userId,
                            CardUserKey = cardUserKey!,
                            CardToken = cardToken!,
                            Brand = cardAssociation ?? "",
                            Last4 = last4 ?? "",
                            Holder = holder
                        }, cancellationToken);
                    }
                }
                catch { /* ignore parsing issues */ }
            }

            var orderTotal = paidPrice; // or basketTotal if your stored order excludes shipping
            var transactionId = ExtractTransactionId(result.RawResponse, paymentModel.ConversationId);

            var order = new Order
            {
                OrderId = GenerateOrderNumber(),
                UserId = userId,
                AddressId = orderAddress.Id,
                Status = "Ödeme alındı",
                OrderDate = DateTime.UtcNow,
                TotalAmount = orderTotal,
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
                        Amount = orderTotal,
                        TransactionDate = DateTime.UtcNow,
                        PaymentMethod = "iyzico",
                        TransactionId = transactionId
                    }
                },
                Shipment = new Shipment
                {
                    Carrier = ResolveCarrier(input.ShippingId),
                    TrackingNumber = string.Empty,
                    ShippedDate = DateTime.UtcNow
                }
            };

            await _orderRepository.InsertOrderAsync(order, cancellationToken);
            await _cartService.RemoveAllAsync(owner, cancellationToken);

            _logger.LogInformation(
                "Iyzico payment completed successfully. ConversationId: {ConversationId}, OrderNumber: {OrderNumber}",
                paymentModel.ConversationId,
                order.OrderId);

            return Ok(new
            {
                success = true,
                message = $"Ödemeniz başarıyla tamamlandı. Sipariş numaranız: {order.OrderId.ToString("D6", CultureInfo.InvariantCulture)}",
                raw = result.RawResponse
            });
        }
        else
        {
            // New guest flow for card payments (Iyzico)
            var guest = ReadGuestInfoFromRequest() ?? FromInput(input);
            if (guest is null)
            {
                return BadRequest(new { success = false, message = "Misafir bilgileri eksik. Lütfen ad, soyad ve adres bilgilerini giriniz." });
            }

            // Only new card is allowed for guests (no saved card capability)
            if (useSaved)
            {
                return BadRequest(new { success = false, message = "Misafir kullanıcılar kayıtlı kart kullanamaz." });
            }

            if (!TryParseCard(input, out var cardInfo, out var cardErrorGuest))
            {
                return BadRequest(new { success = false, message = cardErrorGuest ?? "Kart bilgileri eksik veya hatalı." });
            }

            var lineItems = new List<IyzicoBasketItem>();
            decimal basketTotal = 0m;
            foreach (var item in cart.Items)
            {
                var unitPriceTry = item.UnitPrice * (1 + KdvRate) * fxRate;
                var linePrice = decimal.Round((decimal)(unitPriceTry * item.Quantity), 2, MidpointRounding.AwayFromZero);
                basketTotal += linePrice;
                lineItems.Add(new IyzicoBasketItem
                {
                    Id = item.ProductId.ToString(CultureInfo.InvariantCulture),
                    Name = item.Product?.Name ?? $"Ürün #{item.ProductId}",
                    Price = linePrice
                });
            }

            if (lineItems.Count == 0)
            {
                return BadRequest(new { success = false, message = "Ödeme için sepet ürünü bulunamadı." });
            }

            var paidPrice = basketTotal + shippingPrice;

            var buyerContext = BuildGuestBuyerContext(owner, cart, guest);

            var paymentModel = new IyzicoPaymentModel
            {
                ConversationId = Guid.NewGuid().ToString("N"),
                BasketId = cart.Id.ToString(CultureInfo.InvariantCulture),
                Price = basketTotal,
                PaidPrice = paidPrice,
                Buyer = buyerContext.Buyer,
                BillingAddress = buyerContext.Billing,
                ShippingAddress = buyerContext.Shipping,
                Items = lineItems,
                Card = new IyzicoPaymentCard
                {
                    HolderName = input.Name!.Trim(),
                    Number = SanitizeDigits(input.Number),
                    ExpireMonth = TryParseExpiry(input.Exp, out var m, out var y) ? m : "",
                    ExpireYear = TryParseExpiry(input.Exp, out m, out y) ? y : "",
                    Cvc = SanitizeDigits(input.Cvc),
                    RegisterCard = false
                }
            };

            var result = await _paymentService.CreatePaymentAsync(paymentModel, cancellationToken);
            if (!result.Success)
            {
                var errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                    ? "Ödeme sırasında bir hata oluştu. Lütfen tekrar deneyin."
                    : result.ErrorMessage;
                return StatusCode((int)HttpStatusCode.BadGateway, new { success = false, message = errorMessage });
            }

            var transactionId = ExtractTransactionId(result.RawResponse, paymentModel.ConversationId);

            var order = new Order
            {
                OrderId = GenerateOrderNumber(),
                UserId = null,
                AddressId = null,
                AnonId = cart.AnonId,
                BuyerName = $"{guest.Name} {guest.Surname}".Trim(),
                BuyerEmail = guest.Email,
                BuyerPhone = SanitizeDigits(guest.Phone),
                ShippingAddressText = guest.FullAddress,
                ShippingNeighbourhood = guest.Neighbourhood,
                ShippingState = guest.State,
                ShippingCity = guest.City,
                Status = "Ödeme alındı",
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
                    Carrier = ResolveCarrier(input.ShippingId),
                    TrackingNumber = string.Empty,
                    ShippedDate = DateTime.UtcNow
                }
            };

            await _orderRepository.InsertOrderAsync(order, cancellationToken);
            await _cartService.RemoveAllAsync(owner, cancellationToken);

            _logger.LogInformation(
                "Guest Iyzico payment completed successfully. ConversationId: {ConversationId}, OrderNumber: {OrderNumber}, AnonId: {AnonId}",
                paymentModel.ConversationId,
                order.OrderId,
                cart.AnonId);

            return Ok(new
            {
                success = true,
                message = $"Ödemeniz başarıyla tamamlandı. Sipariş numaranız: {order.OrderId.ToString("D6", CultureInfo.InvariantCulture)}",
                raw = result.RawResponse
            });
        }
    }

    private static decimal CalculateOrderTotal(Cart cart, decimal fxRate)
    {
        decimal total = 0m;
        foreach (var item in cart.Items)
        {
            var unitPriceTry = item.UnitPrice * (1 + KdvRate) * fxRate;
            var linePrice = decimal.Round(unitPriceTry * item.Quantity, 2, MidpointRounding.AwayFromZero);
            total += linePrice;
        }

        return total;
    }

    private static decimal SanitizeShipping(decimal? shipping)
    {
        if (!shipping.HasValue) return DefaultShippingFee;
        if (shipping.Value < 0) return 0m;
        if (shipping.Value > 1000) return DefaultShippingFee; // guard
        return decimal.Round(shipping.Value, 2, MidpointRounding.AwayFromZero);
    }

    private static int GenerateOrderNumber()
        => RandomNumberGenerator.GetInt32(100000, 1_000_000);

    private bool TryResolveOwner(out CartOwner owner, out IActionResult? errorResult)
    {
        owner = default;
        errorResult = null;

        if (User.Identity?.IsAuthenticated == true)
        {
            var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
            if (userIdClaim is not null && int.TryParse(userIdClaim.Value, out var userId))
            {
                owner = CartOwner.FromUser(userId);
                return true;
            }

            errorResult = BadRequest(new { success = false, message = "Kullanıcı kimliği doğrulanamadı." });
            return false;
        }

        if (!Request.Cookies.TryGetValue("ANON_ID", out var anonId) || string.IsNullOrWhiteSpace(anonId))
        {
            errorResult = BadRequest(new { success = false, message = "Misafir sepeti bulunamadı." });
            return false;
        }

        owner = CartOwner.FromAnon(anonId);
        return true;
    }

    private async Task<(IyzicoBuyer Buyer, IyzicoAddress Billing, IyzicoAddress Shipping)> BuildBuyerContextAsync(
        CartOwner owner,
        Cart cart,
        CancellationToken cancellationToken)
    {
        Address? address = null;
        if (owner.UserId is int userId)
        {
            address = await ResolveAddressAsync(userId, cancellationToken);
        }

        var defaultName = address is null ? "Müşteri" : address.Name;
        var defaultSurname = address is null ? "" : address.Surname;
        var contactName = string.Join(' ', new[] { defaultName, defaultSurname }.Where(s => !string.IsNullOrWhiteSpace(s))).Trim();
        if (string.IsNullOrWhiteSpace(contactName))
        {
            contactName = owner.UserId?.ToString(CultureInfo.InvariantCulture) ?? "Misafir";
        }

        var fullAddress = address is null
            ? "Adres belirtilmedi"
            : string.Join(' ', new[]
            {
                address.FullAddress,
                address.Neighbourhood,
                address.State,
                address.City
            }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var phone = NormalizePhone(address?.PhoneNumber) ?? "+905555555555";
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email)) { email = "no-reply@example.com"; }

        var buyer = new IyzicoBuyer
        {
            Id = owner.UserId?.ToString(CultureInfo.InvariantCulture) ?? cart.AnonId ?? Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(defaultName) ? "Müşteri" : defaultName,
            Surname = string.IsNullOrWhiteSpace(defaultSurname) ? "" : defaultSurname,
            GsmNumber = phone,
            Email = email,
            IdentityNumber = DeriveIdentityNumber(address),
            RegistrationAddress = fullAddress,
            City = address?.City ?? "Ankara",
            Country = "Turkey",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1"
        };

        var billing = new IyzicoAddress
        {
            ContactName = contactName,
            City = address?.City ?? "Ankara",
            Address = fullAddress,
            Country = "Turkey"
        };

        var shipping = new IyzicoAddress
        {
            ContactName = contactName,
            City = address?.City ?? "Ankara",
            Address = fullAddress,
            Country = "Turkey"
        };

        return (buyer, billing, shipping);
    }

    private (IyzicoBuyer Buyer, IyzicoAddress Billing, IyzicoAddress Shipping) BuildGuestBuyerContext(
        CartOwner owner, Cart cart, GuestCheckoutViewModel guest)
    {
        var contactName = $"{guest.Name} {guest.Surname}".Trim();
        var fullAddress = string.Join(' ', new[]
        {
            guest.FullAddress,
            guest.Neighbourhood,
            guest.State,
            guest.City
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        var phone = NormalizePhone(guest.Phone) ?? "+905555555555";
        var email = string.IsNullOrWhiteSpace(guest.Email) ? "no-reply@example.com" : guest.Email;

        var buyer = new IyzicoBuyer
        {
            Id = cart.AnonId ?? Guid.NewGuid().ToString("N"),
            Name = string.IsNullOrWhiteSpace(guest.Name) ? "Müşteri" : guest.Name,
            Surname = guest.Surname ?? string.Empty,
            GsmNumber = phone,
            Email = email,
            IdentityNumber = DeriveIdentityNumber(new Address { PhoneNumber = guest.Phone }),
            RegistrationAddress = fullAddress,
            City = string.IsNullOrWhiteSpace(guest.City) ? "Ankara" : guest.City,
            Country = "Turkey",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1"
        };

        var addr = new IyzicoAddress
        {
            ContactName = contactName,
            City = string.IsNullOrWhiteSpace(guest.City) ? "Ankara" : guest.City,
            Address = fullAddress,
            Country = "Turkey"
        };

        return (buyer, addr, addr);
    }

    private async Task<Address?> ResolveAddressAsync(int userId, CancellationToken cancellationToken)
    {
        var addresses = await _addressRepository.GetByUserAsync(userId, cancellationToken);
        return addresses.FirstOrDefault(a => a.IsDefault) ?? addresses.FirstOrDefault();
    }

    private static bool TryParseCard(PlaceOrderInputModel input, out IyzicoPaymentCard card, out string? error)
    {
        card = null!;
        error = null;

        var number = SanitizeDigits(input.Number);
        if (string.IsNullOrWhiteSpace(input.Name) || string.IsNullOrWhiteSpace(number) || string.IsNullOrWhiteSpace(input.Cvc))
        {
            error = "Kart bilgileri eksik.";
            return false;
        }

        if (number.Length < 13 || number.Length > 19)
        {
            error = "Kart numarası geçerli değil.";
            return false;
        }

        var cvc = SanitizeDigits(input.Cvc);
        if (cvc.Length < 3 || cvc.Length > 4)
        {
            error = "CVC bilgisi geçerli değil.";
            return false;
        }

        if (!TryParseExpiry(input.Exp, out var month, out var year))
        {
            error = "Son kullanma tarihi geçerli değil.";
            return false;
        }

        card = new IyzicoPaymentCard
        {
            HolderName = input.Name.Trim(),
            Number = number,
            ExpireMonth = month,
            ExpireYear = year,
            Cvc = cvc,
            RegisterCard = false
        };

        return true;
    }

    private static bool TryParseExpiry(string? exp, out string month, out string year)
    {
        month = string.Empty;
        year = string.Empty;
        if (string.IsNullOrWhiteSpace(exp))
        {
            return false;
        }

        var match = Regex.Match(exp, @"^(?<m>\d{1,2})\s*/\s*(?<y>\d{2,4})$");
        if (!match.Success)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["m"].Value, out var m) || m < 1 || m > 12)
        {
            return false;
        }

        if (!int.TryParse(match.Groups["y"].Value, out var y))
        {
            return false;
        }

        if (y < 100)
        {
            y += 2000;
        }

        month = m.ToString("00", CultureInfo.InvariantCulture);
        year = y.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static string DeriveIdentityNumber(Address? address)
    {
        var digits = SanitizeDigits(address?.PhoneNumber);
        if (!string.IsNullOrWhiteSpace(digits) && digits.Length >= 11)
        {
            return digits[^11..];
        }

        return "11111111111";
    }

    private static string? NormalizePhone(string? phone)
    {
        var digits = SanitizeDigits(phone);
        if (string.IsNullOrWhiteSpace(digits))
        {
            return null;
        }

        if (!digits.StartsWith("90", StringComparison.Ordinal))
        {
            digits = "90" + digits;
        }

        return "+" + digits;
    }

    private static string SanitizeDigits(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        return new string(value.Where(char.IsDigit).ToArray());
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

    private GuestCheckoutViewModel? ReadGuestInfoFromRequest()
    {
        try
        {
            if (Request.Cookies.TryGetValue(GuestInfoCookie, out var json) && !string.IsNullOrWhiteSpace(json))
            {
                var guest = System.Text.Json.JsonSerializer.Deserialize<GuestCheckoutViewModel>(json);
                return guest;
            }
        }
        catch
        {
            // ignore malformed cookie
        }
        return null;
    }

    private static GuestCheckoutViewModel? FromInput(PlaceOrderInputModel input)
    {
        if (string.IsNullOrWhiteSpace(input.GuestName) || string.IsNullOrWhiteSpace(input.GuestSurname) || string.IsNullOrWhiteSpace(input.GuestFullAddress))
        {
            return null;
        }

        return new GuestCheckoutViewModel
        {
            Name = input.GuestName!,
            Surname = input.GuestSurname!,
            Email = input.GuestEmail ?? string.Empty,
            Phone = input.GuestPhone ?? string.Empty,
            City = input.GuestCity ?? string.Empty,
            State = input.GuestState ?? string.Empty,
            Neighbourhood = input.GuestNeighbourhood ?? string.Empty,
            FullAddress = input.GuestFullAddress!
        };
    }

    private static bool IsCardStorageNotEnabled(string? rawResponse)
    {
        if (string.IsNullOrWhiteSpace(rawResponse)) return false;
        try
        {
            using var doc = JsonDocument.Parse(rawResponse);
            var root = doc.RootElement;
            // errorCode can be in different casing depending on serializer
            if (root.TryGetProperty("errorCode", out var ec))
            {
                if (ec.ValueKind == JsonValueKind.String && string.Equals(ec.GetString(), "3007", StringComparison.Ordinal)) return true;
                if (ec.ValueKind == JsonValueKind.Number && ec.TryGetInt32(out var eci) && eci == 3007) return true;
            }
            if (root.TryGetProperty("ErrorCode", out var ec2))
            {
                if (ec2.ValueKind == JsonValueKind.String && string.Equals(ec2.GetString(), "3007", StringComparison.Ordinal)) return true;
                if (ec2.ValueKind == JsonValueKind.Number && ec2.TryGetInt32(out var eci2) && eci2 == 3007) return true;
            }
            if (root.TryGetProperty("errorMessage", out var em) && em.ValueKind == JsonValueKind.String)
            {
                var msg = em.GetString();
                if (!string.IsNullOrWhiteSpace(msg) && msg.Contains("kart saklama", StringComparison.OrdinalIgnoreCase)) return true;
            }
            if (root.TryGetProperty("ErrorMessage", out var em2) && em2.ValueKind == JsonValueKind.String)
            {
                var msg = em2.GetString();
                if (!string.IsNullOrWhiteSpace(msg) && msg.Contains("kart saklama", StringComparison.OrdinalIgnoreCase)) return true;
            }
        }
        catch
        {
            // ignore parse errors, fallback to substring check
        }
        // last resort substring check
        return rawResponse.Contains("3007", StringComparison.Ordinal);
    }
}
