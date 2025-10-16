using ECommerceBatteryShop.Options;              // IyzicoOptions, IyzicoDefaults
using Iyzipay;
using Iyzipay.Model;
using Iyzipay.Request;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Configuration;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace ECommerceBatteryShop.Services;

public interface IIyzicoPaymentService
{
    Task<IyzicoPaymentResult> CreatePaymentAsync(IyzicoPaymentModel model, CancellationToken cancellationToken = default);
    Task<IyzicoThreeDSInitializeResult> InitializeThreeDSPaymentAsync(IyzicoPaymentModel model, CancellationToken cancellationToken = default);
    Task<IyzicoPaymentResult> CompleteThreeDSPaymentAsync(IyzicoThreeDSCompleteModel model, CancellationToken cancellationToken = default);
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

        var req = new CreatePaymentRequest
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

        // Card: raw or saved
        if (model.Card is not null)
        {
            req.PaymentCard = new PaymentCard
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
            req.PaymentCard = new PaymentCard
            {
                CardUserKey = model.Saved.CardUserKey,
                CardToken = model.Saved.CardToken
            };
        }

        if (!string.IsNullOrWhiteSpace(_settings.CallbackUrl))
            req.CallbackUrl = _settings.CallbackUrl;

        var defaults = _configuration.GetSection("IyzicoDefaults").Get<IyzicoDefaults>() ?? new();

        req.Buyer = new Buyer
        {
            Id = model.Buyer.Id,
            Name = model.Buyer.Name,
            Surname = model.Buyer.Surname,
            GsmNumber = model.Buyer.GsmNumber,
            Email = model.Buyer.Email,
            IdentityNumber = model.Buyer.IdentityNumber,
            RegistrationAddress = model.Buyer.RegistrationAddress,
            City = model.Buyer.City,
            Country = string.IsNullOrWhiteSpace(model.Buyer.Country) ? defaults.Country : model.Buyer.Country,
            Ip = model.Buyer.Ip
        };

        // <<< fully qualify Address and use Address=... field
        req.ShippingAddress = new Iyzipay.Model.Address
        {
            ContactName = model.ShippingAddress.ContactName,
            City = model.ShippingAddress.City,
            Country = string.IsNullOrWhiteSpace(model.ShippingAddress.Country) ? defaults.Country : model.ShippingAddress.Country,
            Description = string.IsNullOrWhiteSpace(model.ShippingAddress.Address) ? "Adres belirtilmedi" : model.ShippingAddress.Address
        };

        req.BillingAddress = new Iyzipay.Model.Address
        {
            ContactName = model.BillingAddress.ContactName,
            City = model.BillingAddress.City,
            Country = string.IsNullOrWhiteSpace(model.BillingAddress.Country) ? defaults.Country : model.BillingAddress.Country,
            Description = string.IsNullOrWhiteSpace(model.BillingAddress.Address) ? "Adres belirtilmedi" : model.BillingAddress.Address
        };

        req.BasketItems = model.Items.Select(item => new BasketItem
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
            var resp = await Task.Run(() => Payment.Create(req, _options), cancellationToken);
            var ok = string.Equals(resp.Status, "success", StringComparison.OrdinalIgnoreCase);

            if (!ok)
            {
                _logger.LogWarning("Iyzico payment failed. ConversationId: {ConversationId}, ErrorCode: {ErrorCode}, Message: {Message}",
                    model.ConversationId, resp.ErrorCode, resp.ErrorMessage);
            }

            var rawJson = SafeSerialize(resp);
            return new IyzicoPaymentResult(ok, resp.ErrorMessage, rawJson);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Error while creating Iyzico payment. ConversationId: {ConversationId}", model.ConversationId);
            return new IyzicoPaymentResult(false, ex.Message, null);
        }
    }


// INIT 3DS
public async Task<IyzicoThreeDSInitializeResult> InitializeThreeDSPaymentAsync(
    IyzicoPaymentModel model, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(model);
    ct.ThrowIfCancellationRequested();

    var callbackUrl = _settings.ThreeDSCallbackUrl ?? _settings.CallbackUrl;
    if (string.IsNullOrWhiteSpace(callbackUrl))
        return new(false, "3D Secure callback adresi yapılandırılmamış.", null, null);

    var req = new CreatePaymentRequest
    {
        Locale = Locale.TR.ToString(),
        ConversationId = model.ConversationId,
        Price = ToPrice(model.Price),         // "0.00"
        PaidPrice = ToPrice(model.PaidPrice), // "0.00"
        BasketId = model.BasketId,
        Installment = model.Installment,
        Currency = model.Currency,
        PaymentChannel = model.PaymentChannel,
        PaymentGroup = model.PaymentGroup,
        CallbackUrl = callbackUrl
    };

    // payment card (raw or saved)
    if (model.Card is not null)
    {
        req.PaymentCard = new PaymentCard
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
        req.PaymentCard = new PaymentCard
        {
            CardUserKey = model.Saved.CardUserKey,
            CardToken = model.Saved.CardToken
        };
    }

    var defaults = _configuration.GetSection("IyzicoDefaults").Get<IyzicoDefaults>() ?? new();

    req.Buyer = new Buyer
    {
        Id = model.Buyer.Id,
        Name = model.Buyer.Name,
        Surname = model.Buyer.Surname,
        GsmNumber = model.Buyer.GsmNumber,
        Email = model.Buyer.Email,
        IdentityNumber = model.Buyer.IdentityNumber,
        RegistrationAddress = model.Buyer.RegistrationAddress,
        City = model.Buyer.City,
        Country = string.IsNullOrWhiteSpace(model.Buyer.Country) ? defaults.Country : model.Buyer.Country,
        Ip = model.Buyer.Ip
    };

    req.ShippingAddress = new Iyzipay.Model.Address
    {
        ContactName = model.ShippingAddress.ContactName,
        City = model.ShippingAddress.City,
        Country = string.IsNullOrWhiteSpace(model.ShippingAddress.Country) ? defaults.Country : model.ShippingAddress.Country,
        Description = string.IsNullOrWhiteSpace(model.ShippingAddress.Address) ? "Adres belirtilmedi" : model.ShippingAddress.Address
    };
    req.BillingAddress = new Iyzipay.Model.Address
    {
        ContactName = model.BillingAddress.ContactName,
        City = model.BillingAddress.City,
        Country = string.IsNullOrWhiteSpace(model.BillingAddress.Country) ? defaults.Country : model.BillingAddress.Country,
        Description = string.IsNullOrWhiteSpace(model.BillingAddress.Address) ? "Adres belirtilmedi" : model.BillingAddress.Address
    };

    req.BasketItems = model.Items.Select(i => new BasketItem
    {
        Id = i.Id,
        Name = i.Name,
        Category1 = i.Category1,
        Category2 = i.Category2,
        ItemType = i.ItemType,
        Price = ToPrice(i.Price)
    }).ToList();

    try
    {
        // 3DS INIT
        var resp = await Task.Run(() => ThreedsInitialize.Create(req, _options), ct);
        var ok = string.Equals(resp.Status, "success", StringComparison.OrdinalIgnoreCase);

        string? html = null;
        if (ok && !string.IsNullOrWhiteSpace(resp.HtmlContent))
        {
            html = DecodeBase64ToUtf8Safe(resp.HtmlContent);
        }

        if (ok && string.IsNullOrWhiteSpace(html))
        {
            // Fallback: try to parse from raw response JSON (may contain HtmlContent as string)
            var raw = SafeSerialize(resp);
            try
            {
                using var doc = JsonDocument.Parse(raw);
                var root = doc.RootElement;
                if (root.TryGetProperty("HtmlContent", out var hc) || root.TryGetProperty("htmlContent", out hc))
                {
                    var base64 = hc.GetString();
                    html = DecodeBase64ToUtf8Safe(base64);
                }
            }
            catch(Exception ex)
            {
               Console.WriteLine("Hata: ", ex);
            }
        }

        if (!ok)
        {
            _logger.LogWarning("3DS init failed. ConversationId: {Conv}, ErrorCode: {ErrorCode}, Message: {Message}", model.ConversationId, resp.ErrorCode, resp.ErrorMessage);
        }

        return new(ok, resp.ErrorMessage, html, SafeSerialize(resp));
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogError(ex, "3DS init error. ConversationId:{Conv}", model.ConversationId);
        return new(false, ex.Message, null, null);
    }
}

// COMPLETE 3DS
public async Task<IyzicoPaymentResult> CompleteThreeDSPaymentAsync(
    IyzicoThreeDSCompleteModel model, CancellationToken ct = default)
{
    ArgumentNullException.ThrowIfNull(model);
    ct.ThrowIfCancellationRequested();

    var req = new CreateThreedsPaymentRequest
    {
        Locale = Locale.TR.ToString(),
        ConversationId = model.ConversationId,
        PaymentId = model.PaymentId,
        ConversationData = model.ConversationData
    };

    try
    {
        var resp = await Task.Run(() => ThreedsPayment.Create(req, _options), ct);
        var ok = string.Equals(resp.Status, "success", StringComparison.OrdinalIgnoreCase);
        return new(ok, resp.ErrorMessage, JsonSerializer.Serialize(resp));
    }
    catch (Exception ex) when (ex is not OperationCanceledException)
    {
        _logger.LogError(ex, "3DS complete error. ConversationId:{Conv}", model.ConversationId);
        return new(false, ex.Message, null);
    }
}

// helper
private static string ToPrice(decimal value) =>
    decimal.Round(value, 2, MidpointRounding.AwayFromZero).ToString("0.00", CultureInfo.InvariantCulture);


private static string SafeSerialize(object resp)
    {
        try { return JsonSerializer.Serialize(resp); }
        catch { return resp?.ToString() ?? "{}"; }
    }

    private static string? DecodeBase64ToUtf8Safe(string? s)
    {
        if (string.IsNullOrWhiteSpace(s)) return null;

        // Normalize possible URL-safe base64 and add padding if needed
        var normalized = s.Trim().Replace('-', '+').Replace('_', '/');
        int mod4 = normalized.Length % 4;
        if (mod4 > 0)
        {
            normalized = normalized.PadRight(normalized.Length + (4 - mod4), '=');
        }

        try
        {
            var bytes = Convert.FromBase64String(normalized);
            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return null; // do not throw, let caller handle null
        }
    }
}

// ====== Records / models (kept here to resolve CS0246) ======

public record IyzicoPaymentResult(bool Success, string? ErrorMessage, string? RawResponse);

public record IyzicoThreeDSInitializeResult(bool Success, string? ErrorMessage, string? HtmlContent, string? RawResponse);

public sealed record IyzicoThreeDSCompleteModel(
    string PaymentId,
    string ConversationId,
    string? ConversationData
);

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