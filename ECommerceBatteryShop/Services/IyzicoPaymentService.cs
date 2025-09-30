using System.Collections.Generic;
using System.Globalization;
using ECommerceBatteryShop.Options;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ECommerceBatteryShop.Services;

public class IyzicoPaymentService : IIyzicoPaymentService
{
    private readonly IOptions<IyzicoOptions> _options;
    private readonly ILogger<IyzicoPaymentService> _logger;

    public IyzicoPaymentService(IOptions<IyzicoOptions> options, ILogger<IyzicoPaymentService> logger)
    {
        _options = options;
        _logger = logger;
    }

    public Task<string?> InitializeCheckoutFormAsync(IyzicoCheckoutContext context, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(context.Cart);

        if (ct.IsCancellationRequested)
        {
            return Task.FromCanceled<string?>(ct);
        }

        if (context.Cart.Items.Count == 0)
        {
            return Task.FromResult<string?>(null);
        }

        var settings = _options.Value;
        if (string.IsNullOrWhiteSpace(settings.ApiKey) ||
            string.IsNullOrWhiteSpace(settings.SecretKey) ||
            string.IsNullOrWhiteSpace(settings.BaseUrl))
        {
            _logger.LogWarning("Iyzipay credentials are missing. Checkout form will not be generated.");
            return Task.FromResult<string?>(null);
        }

        var options = new Iyzipay.Options
        {
            ApiKey = settings.ApiKey,
            SecretKey = settings.SecretKey,
            BaseUrl = settings.BaseUrl
        };

        var callbackUrl = context.CallbackUrl;
        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            callbackUrl = settings.CallbackUrl;
        }

        if (string.IsNullOrWhiteSpace(callbackUrl))
        {
            callbackUrl = "https://localhost/iyzico/callback";
        }

        var request = new CreateCheckoutFormInitializeRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = context.ConversationId ?? Guid.NewGuid().ToString("N"),
            Price = context.ItemsTotal.ToString("0.00", CultureInfo.InvariantCulture),
            PaidPrice = (context.ItemsTotal + context.ShippingCost).ToString("0.00", CultureInfo.InvariantCulture),
            Currency = Currency.TRY.ToString(),
            BasketId = context.BasketId ?? context.Cart.Id.ToString(CultureInfo.InvariantCulture),
            PaymentGroup = PaymentGroup.PRODUCT.ToString(),
            CallbackUrl = callbackUrl,
            EnabledInstallments = new List<int> { 1, 2, 3, 6, 9, 12 }
        };

        var buyerInfo = context.Buyer ?? new IyzicoBuyerInfo();
        var buyer = new Buyer
        {
            Id = buyerInfo.Id ?? context.Cart.UserId?.ToString(CultureInfo.InvariantCulture) ?? context.Cart.AnonId ?? "guest",
            Name = string.IsNullOrWhiteSpace(buyerInfo.FirstName) ? "Müşteri" : buyerInfo.FirstName,
            Surname = string.IsNullOrWhiteSpace(buyerInfo.LastName) ? "Bilinmiyor" : buyerInfo.LastName,
            Email = string.IsNullOrWhiteSpace(buyerInfo.Email) ? "info@dayilyenerji.com" : buyerInfo.Email,
            GsmNumber = string.IsNullOrWhiteSpace(buyerInfo.PhoneNumber) ? "+900000000000" : buyerInfo.PhoneNumber,
            IdentityNumber = string.IsNullOrWhiteSpace(buyerInfo.IdentityNumber) ? "11111111111" : buyerInfo.IdentityNumber,
            RegistrationAddress = string.IsNullOrWhiteSpace(buyerInfo.AddressLine) ? "Adres belirtilmedi" : buyerInfo.AddressLine,
            City = string.IsNullOrWhiteSpace(buyerInfo.City) ? "Istanbul" : buyerInfo.City,
            Country = string.IsNullOrWhiteSpace(buyerInfo.Country) ? "Turkey" : buyerInfo.Country,
            ZipCode = string.IsNullOrWhiteSpace(buyerInfo.ZipCode) ? "00000" : buyerInfo.ZipCode,
            Ip = string.IsNullOrWhiteSpace(context.BuyerIpAddress) ? "127.0.0.1" : context.BuyerIpAddress
        };
        request.Buyer = buyer;

        var addressDescription = string.IsNullOrWhiteSpace(buyerInfo.AddressLine) ? "Adres belirtilmedi" : buyerInfo.AddressLine;
        var contactName = $"{buyer.Name} {buyer.Surname}".Trim();
        var shippingAddress = new Address
        {
            ContactName = string.IsNullOrWhiteSpace(contactName) ? "Müşteri" : contactName,
            City = string.IsNullOrWhiteSpace(buyerInfo.City) ? "Istanbul" : buyerInfo.City,
            Country = string.IsNullOrWhiteSpace(buyerInfo.Country) ? "Turkey" : buyerInfo.Country,
            ZipCode = string.IsNullOrWhiteSpace(buyerInfo.ZipCode) ? "00000" : buyerInfo.ZipCode,
            Description = addressDescription
        };

        request.ShippingAddress = shippingAddress;
        request.BillingAddress = new Address
        {
            ContactName = shippingAddress.ContactName,
            City = shippingAddress.City,
            Country = shippingAddress.Country,
            ZipCode = shippingAddress.ZipCode,
            Description = addressDescription
        };

        var basketItems = new List<BasketItem>();
        foreach (var item in context.Cart.Items)
        {
            var linePrice = decimal.Round(item.UnitPrice * item.Quantity * 1.2m * context.FxRate, 2, MidpointRounding.AwayFromZero);
            if (linePrice <= 0)
            {
                continue;
            }

            basketItems.Add(new BasketItem
            {
                Id = item.ProductId.ToString(CultureInfo.InvariantCulture),
                Name = item.Product?.Name ?? $"Ürün #{item.ProductId}",
                Category1 = "Batarya",
                ItemType = BasketItemType.PHYSICAL.ToString(),
                Price = linePrice.ToString("0.00", CultureInfo.InvariantCulture)
            });
        }

        if (context.ShippingCost > 0)
        {
            basketItems.Add(new BasketItem
            {
                Id = "SHIPPING",
                Name = "Kargo",
                Category1 = "Lojistik",
                ItemType = BasketItemType.VIRTUAL.ToString(),
                Price = context.ShippingCost.ToString("0.00", CultureInfo.InvariantCulture)
            });
        }

        if (basketItems.Count == 0)
        {
            // Iyzipay requires at least one item; fall back to a dummy item.
            basketItems.Add(new BasketItem
            {
                Id = "DUMMY",
                Name = "Sepet",
                Category1 = "Batarya",
                ItemType = BasketItemType.PHYSICAL.ToString(),
                Price = context.ItemsTotal.ToString("0.00", CultureInfo.InvariantCulture)
            });
        }

        request.BasketItems = basketItems;

        var checkoutForm = CheckoutFormInitialize.Create(request, options);
        if (checkoutForm is null)
        {
            _logger.LogWarning("Iyzipay checkout form response was null.");
            return Task.FromResult<string?>(null);
        }

        if (string.Equals(checkoutForm.Status, "failure", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Iyzipay returned failure status: {Message}", checkoutForm.ErrorMessage);
            return Task.FromResult<string?>(null);
        }

        return Task.FromResult(checkoutForm.CheckoutFormContent);
    }
}
