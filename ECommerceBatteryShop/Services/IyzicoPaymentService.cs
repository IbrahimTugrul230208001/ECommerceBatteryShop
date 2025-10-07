using ECommerceBatteryShop.Options;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Options;
using System.Globalization;
using System.Linq;
using System.Text.Json;

namespace ECommerceBatteryShop.Services;

public interface IIyzicoPaymentService
{
    Task<IyzicoPaymentResult> CreatePaymentAsync(IyzicoPaymentModel model, CancellationToken cancellationToken = default);
}

public class IyzicoPaymentService : IIyzicoPaymentService
{
    private readonly IConfiguration _configuration;
    private readonly Iyzipay.Options _options;
    private readonly ILogger<IyzicoPaymentService> _logger;
    private readonly IyzicoOptions _settings;

    public IyzicoPaymentService(IOptions<IyzicoOptions> options, ILogger<IyzicoPaymentService> logger, IConfiguration configuration)
    {
        _settings = options.Value ?? throw new ArgumentNullException(nameof(options));
        _options = new Iyzipay.Options
        {
            ApiKey = _settings.ApiKey,
            SecretKey = _settings.SecretKey,
            BaseUrl = _settings.BaseUrl
        };
        _logger = logger;
        _configuration = configuration;
    }


    public async Task<IyzicoPaymentResult> CreatePaymentAsync(IyzicoPaymentModel model, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(model);
        cancellationToken.ThrowIfCancellationRequested();

        var request = new CreatePaymentRequest
        {
            Locale = Locale.TR.ToString(),
            ConversationId = model.ConversationId,
            Price = ToPrice(model.Price),
            PaidPrice = ToPrice(model.PaidPrice),
            BasketId = model.BasketId,
            Installment = model.Installment,
            Currency = model.Currency,
            PaymentChannel = model.PaymentChannel,
            PaymentGroup = model.PaymentGroup
        };

        // Either raw card or saved card
        if (model.Card is not null)
        {
            request.PaymentCard = new PaymentCard
            {
                CardHolderName = model.Card.HolderName,
                CardNumber = model.Card.Number,
                ExpireMonth = model.Card.ExpireMonth,
                ExpireYear = model.Card.ExpireYear,
                Cvc = model.Card.Cvc,
                RegisterCard = model.Card.RegisterCard ? 1 : 0
            };
        }
        else if (model.Saved is not null)
        {
            request.PaymentCard = new PaymentCard
            {
                CardUserKey = model.Saved.CardUserKey,
                CardToken = model.Saved.CardToken
            };
        }

        if (!string.IsNullOrWhiteSpace(_settings.CallbackUrl))
        {
            request.CallbackUrl = _settings.CallbackUrl;
        }

        var defaults = _configuration.GetSection("IyzicoDefaults").Get<IyzicoDefaults>() ?? new();

        request.Buyer = new Buyer
        {
            Id = model.Buyer.Id,
            Name = model.Buyer.Name,
            Surname = model.Buyer.Surname,
            GsmNumber = model.Buyer.GsmNumber,
            Email = model.Buyer.Email,
            IdentityNumber = model.Buyer.IdentityNumber,
            RegistrationAddress = model.Buyer.RegistrationAddress,
            City = model.Buyer.City,
            Country = string.IsNullOrWhiteSpace(model.Buyer.Country)
                ? defaults.Country
                : model.Buyer.Country,
            Ip = model.Buyer.Ip
        };
        request.ShippingAddress = new Iyzipay.Model.Address
        {
            ContactName = model.ShippingAddress.ContactName,
            City = model.ShippingAddress.City,
            Country = string.IsNullOrWhiteSpace(model.ShippingAddress.Country)
                ? defaults.Country
                : model.ShippingAddress.Country,
            Description = string.IsNullOrWhiteSpace(model.ShippingAddress.Address)
                ? "Adres belirtilmedi"
                : model.ShippingAddress.Address
        };
        request.BillingAddress = new Iyzipay.Model.Address
        {
            ContactName = model.BillingAddress.ContactName,
            City = model.BillingAddress.City,
            Country = string.IsNullOrWhiteSpace(model.BillingAddress.Country)
                ? defaults.Country
                : model.BillingAddress.Country,
            Description = string.IsNullOrWhiteSpace(model.BillingAddress.Address)
                ? "Adres belirtilmedi"
                : model.BillingAddress.Address
        };
        request.Buyer.Country = string.IsNullOrWhiteSpace(request.Buyer.Country)
            ? defaults.Country
            : request.Buyer.Country;
        request.BasketItems = model.Items.Select(item => new BasketItem
        {
            Id = item.Id,
            Name = item.Name,
            Category1 = item.Category1,
            Category2 = item.Category2,
            ItemType = item.ItemType,
            Price = ToPrice(item.Price)
        }).ToList();

        try
        {
            var response = await Task.Run(() => Payment.Create(request, _options), cancellationToken);
            var success = string.Equals(response.Status, "success", StringComparison.OrdinalIgnoreCase);

            var rawJson = JsonSerializer.Serialize(response);
            if (!success)
            {
                _logger.LogWarning("Iyzico payment failed. ConversationId: {ConversationId}, ErrorCode: {ErrorCode}, Message: {Message}",
                    model.ConversationId,
                    response.ErrorCode,
                    response.ErrorMessage);
            }

            return new IyzicoPaymentResult(success, response.ErrorMessage, rawJson);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error while creating Iyzico payment. ConversationId: {ConversationId}", model.ConversationId);
            return new IyzicoPaymentResult(false, ex.Message, null);
        }
    }

    private static string ToPrice(decimal value)
        => decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);
}

public record IyzicoPaymentResult(bool Success, string? ErrorMessage, string? RawResponse);

public class IyzicoPaymentModel
{
    public required string ConversationId { get; init; }
    public required string BasketId { get; init; }
    public required decimal Price { get; init; }
    public required decimal PaidPrice { get; init; }
    public int Installment { get; init; } = 1;
    public string Currency { get; init; } = "TRY";
    public string PaymentChannel { get; init; } = "WEB";
    public string PaymentGroup { get; init; } = "PRODUCT";
    public IyzicoPaymentCard? Card { get; set; }
    public IyzicoSavedCard? Saved { get; set; }
    public required IyzicoBuyer Buyer { get; init; }
    public required IyzicoAddress BillingAddress { get; init; }
    public required IyzicoAddress ShippingAddress { get; init; }
    public required IReadOnlyList<IyzicoBasketItem> Items { get; init; }
}

public class IyzicoPaymentCard
{
    public required string HolderName { get; init; }
    public required string Number { get; init; }
    public required string ExpireMonth { get; init; }
    public required string ExpireYear { get; init; }
    public required string Cvc { get; init; }
    public bool RegisterCard { get; set; }
}

public class IyzicoSavedCard
{
    public required string CardUserKey { get; init; }
    public required string CardToken { get; init; }
}


public class IyzicoBuyer
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Surname { get; init; }
    public required string GsmNumber { get; init; }
    public required string Email { get; init; }
    public required string IdentityNumber { get; init; }
    public required string RegistrationAddress { get; init; }
    public required string City { get; init; } = "Ankara";
    public required string Country { get; init; } = "Turkey";
    public string Ip { get; init; } = "";
}

public class IyzicoAddress
{
    public required string ContactName { get; init; }
    public required string City { get; init; }
    public required string Address { get; init; }
    public required string Country { get; init; }
}

public class IyzicoBasketItem
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public string Category1 { get; init; } = "Elektronik";
    public string Category2 { get; init; } = "Batarya";
    public string ItemType { get; init; } = BasketItemType.PHYSICAL.ToString();
    public required decimal Price { get; init; }
}
