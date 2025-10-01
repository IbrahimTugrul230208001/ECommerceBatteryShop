using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers;

[Authorize]
[AutoValidateAntiforgeryToken]
public class CheckoutController : Controller
{
    private const decimal DefaultExchangeRate = 41.3m;
    private const decimal KdvRate = 0.20m;
    private const decimal ShippingFee = 150m;

    private readonly ICartService _cartService;
    private readonly ICurrencyService _currencyService;
    private readonly IAddressRepository _addressRepository;
    private readonly IIyzicoPaymentService _paymentService;
    private readonly ILogger<CheckoutController> _logger;

    public CheckoutController(
        ICartService cartService,
        ICurrencyService currencyService,
        IAddressRepository addressRepository,
        IIyzicoPaymentService paymentService,
        ILogger<CheckoutController> logger)
    {
        _cartService = cartService;
        _currencyService = currencyService;
        _addressRepository = addressRepository;
        _paymentService = paymentService;
        _logger = logger;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> PlaceOrder([FromForm] PlaceOrderInputModel input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            var firstError = ModelState.Values.SelectMany(v => v.Errors).FirstOrDefault()?.ErrorMessage
                            ?? "Geçersiz istek.";
            return BadRequest(new { success = false, message = firstError });
        }

        if (string.Equals(input.PaymentMethod, "iban", StringComparison.OrdinalIgnoreCase))
        {
            return Ok(new
            {
                success = true,
                message = "Havale/EFT seçildi. Siparişinizi tamamlamak için belirtilen IBAN’a ödeme yapabilirsiniz."
            });
        }

        if (!string.Equals(input.PaymentMethod, "card_new", StringComparison.OrdinalIgnoreCase))
        {
            return BadRequest(new { success = false, message = "Seçilen ödeme yöntemi şu anda desteklenmiyor." });
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

        if (!TryParseCard(input, out var cardInfo, out var cardError))
        {
            return BadRequest(new { success = false, message = cardError ?? "Kart bilgileri eksik veya hatalı." });
        }

        var fxRate = await _currencyService.GetCachedUsdTryAsync(cancellationToken) ?? DefaultExchangeRate;

        var lineItems = new List<IyzicoBasketItem>();
        decimal basketTotal = 0m;
        foreach (var item in cart.Items)
        {
            var unitPriceTry = item.UnitPrice * (1 + KdvRate) * fxRate;
            var linePrice = decimal.Round(unitPriceTry * item.Quantity, 2, MidpointRounding.AwayFromZero);
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

        var paidPrice = basketTotal + ShippingFee;

        var buyerContext = await BuildBuyerContextAsync(owner, cart, cancellationToken);

        var paymentModel = new IyzicoPaymentModel
        {
            ConversationId = Guid.NewGuid().ToString("N"),
            BasketId = cart.Id.ToString(CultureInfo.InvariantCulture),
            Price = basketTotal,
            PaidPrice = paidPrice,
            Card = cardInfo,
            Buyer = buyerContext.Buyer,
            BillingAddress = buyerContext.Billing,
            ShippingAddress = buyerContext.Shipping,
            Items = lineItems
        };

        var result = await _paymentService.CreatePaymentAsync(paymentModel, cancellationToken);
        if (!result.Success)
        {
            var errorMessage = string.IsNullOrWhiteSpace(result.ErrorMessage)
                ? "Ödeme sırasında bir hata oluştu. Lütfen tekrar deneyin."
                : result.ErrorMessage;
            return StatusCode((int)HttpStatusCode.BadGateway, new { success = false, message = errorMessage });
        }

        await _cartService.RemoveAllAsync(owner, cancellationToken);
        _logger.LogInformation("Iyzico payment completed successfully. ConversationId: {ConversationId}", paymentModel.ConversationId);

        return Ok(new
        {
            success = true,
            message = "Ödemeniz başarıyla tamamlandı. Siparişiniz işleme alındı.",
            raw = result.RawResponse
        });
    }

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

        var phone = NormalizePhone(address?.PhoneNumber) ?? "+900000000000";
        var email = User.FindFirst(System.Security.Claims.ClaimTypes.Email)?.Value;
        if (string.IsNullOrWhiteSpace(email))
        {
            email = "info@dayilyenerji.com";
        }

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
            ZipCode = "00000",
            Ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1"
        };

        var billing = new IyzicoAddress
        {
            ContactName = contactName,
            City = address?.City ?? "Ankara",
            Country = "Turkey",
            Description = fullAddress,
            ZipCode = "00000"
        };

        var shipping = new IyzicoAddress
        {
            ContactName = contactName,
            City = address?.City ?? "Ankara",
            Country = "Turkey",
            Description = fullAddress,
            ZipCode = "00000"
        };

        return (buyer, billing, shipping);
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
            RegisterCard = input.Save
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
}
