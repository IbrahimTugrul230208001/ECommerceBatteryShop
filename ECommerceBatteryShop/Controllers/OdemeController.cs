using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using ECommerceBatteryShop.Options;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Primitives; // for StringValues

namespace ECommerceBatteryShop.Controllers;

public class OdemeController : Controller
{
    private readonly ICartService _cartService;
    private readonly IyzicoOptions _iyzicoOptions;
    private readonly ILogger<OdemeController> _logger;

    public OdemeController(
        ICartService cartService,
        IOptions<IyzicoOptions> iyzicoOptions,
        ILogger<OdemeController> logger)
    {
        _cartService = cartService;
        _iyzicoOptions = iyzicoOptions.Value;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> IyzicoCallback(CancellationToken cancellationToken = default)
    {
        Request.EnableBuffering();
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }
        Request.Body.Position = 0;

        // Accept common header name variants used by IyziCo webhooks
        static string? GetSignatureHeader(IHeaderDictionary headers)
        {
            // Header names are case-insensitive, but include common alternates to be safe
            string[] candidates = new[]
            {
                "X-IYZ-Signature",   // most common, official docs
                "x-iyz-signature",
                "IYZ-Signature",
                "iyz-signature",
                "X-IYZI-Signature",  // some community samples use iyzi spelling
                "x-iyzi-signature",
                "iyzi-signature",
                "iyziSignature"
            };

            foreach (var name in candidates)
            {
                if (headers.TryGetValue(name, out var value) && !StringValues.IsNullOrEmpty(value))
                {
                    return value.ToString();
                }
            }
            return null;
        }

        async Task<string?> TryGetSignatureFromFormAsync()
        {
            if (!Request.HasFormContentType)
            {
                return null;
            }

            var form = await Request.ReadFormAsync(cancellationToken);
            Request.Body.Position = 0;

            return ExtractSignatureFromFormCollection(form);
        }

        string? signature = GetSignatureHeader(Request.Headers);
        JsonDocument? document = null;
        JsonException? parseException = null;
        JsonElement root = default;
        var hasJson = false;

        if (string.IsNullOrWhiteSpace(signature))
        {
            signature = await TryGetSignatureFromFormAsync();
        }

        if (!string.IsNullOrWhiteSpace(payload))
        {
            try
            {
                document = JsonDocument.Parse(payload);
                root = document.RootElement;
                hasJson = true;
                if (string.IsNullOrWhiteSpace(signature))
                {
                    signature = ExtractSignatureFromJson(root);
                }
            }
            catch (JsonException ex)
            {
                parseException = ex;
            }
        }

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Iyzico callback received without a signature. Skipping validation.");
        }
        else if (!VerifySignature(payload, signature))
        {
            _logger.LogWarning("Iyzico callback signature validation failed.");
            document?.Dispose();
            return Unauthorized();
        }

        if (hasJson)
        {
            var status = root.TryGetProperty("status", out var statusElement) && statusElement.ValueKind == JsonValueKind.String
                ? statusElement.GetString()
                : null;
            var conversationId = root.TryGetProperty("conversationId", out var conversationIdElement) && conversationIdElement.ValueKind == JsonValueKind.String
                ? conversationIdElement.GetString()
                : null;
            var paymentId = root.TryGetProperty("paymentId", out var paymentIdElement)
                ? paymentIdElement.ToString()
                : null;

            _logger.LogInformation(
                "Received Iyzico callback. Status: {Status}, ConversationId: {ConversationId}, PaymentId: {PaymentId}",
                status,
                conversationId,
                paymentId);
        }
        else if (parseException is not null)
        {
            _logger.LogWarning(parseException, "Unable to parse Iyzico callback payload.");
        }

        document?.Dispose();

        return Ok(new { success = true });
    }

    private bool VerifySignature(string payload, string providedSignature)
    {
        if (string.IsNullOrEmpty(_iyzicoOptions.SecretKey))
        {
            _logger.LogWarning("Iyzico secret key is not configured. Skipping signature validation.");
            return true;
        }

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_iyzicoOptions.SecretKey));
        var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload ?? string.Empty));
        var computedSignature = Convert.ToBase64String(computedHash);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(computedSignature),
            Encoding.UTF8.GetBytes(providedSignature));
    }

    private static string? ExtractSignatureFromJson(JsonElement root)
    {
        string[] propertyCandidates = new[]
        {
            "iyziSignature",
            "signature",
            "IyziSignature",
            "Iyzi-Signature",
            "iyzi-signature"
        };

        foreach (var propertyName in propertyCandidates)
        {
            if (root.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String)
            {
                var value = property.GetString();
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value.Trim();
                }
            }
        }

        return null;
    }

    private static string? ExtractSignatureFromFormCollection(IFormCollection form)
    {
        string[] formKeys = new[]
        {
            "iyziSignature",
            "signature",
            "IyziSignature",
            "Iyzi-Signature",
            "iyzi-signature"
        };

        foreach (var key in formKeys)
        {
            if (form.TryGetValue(key, out var value) && !StringValues.IsNullOrEmpty(value))
            {
                var signature = value.ToString();
                if (!string.IsNullOrWhiteSpace(signature))
                {
                    return signature.Trim();
                }
            }
        }

        return null;
    }
}
