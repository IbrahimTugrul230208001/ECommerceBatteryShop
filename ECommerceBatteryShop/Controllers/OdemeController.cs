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

        var signature = GetSignatureHeader(Request.Headers);

        if (string.IsNullOrWhiteSpace(signature))
        {
            _logger.LogWarning("Iyzico callback rejected because signature header is missing.");
            return Unauthorized();
        }

        if (!VerifySignature(payload, signature))
        {
            _logger.LogWarning("Iyzico callback signature validation failed.");
            return Unauthorized();
        }

        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(payload) ? "{}" : payload);
            var root = document.RootElement;
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
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Unable to parse Iyzico callback payload.");
        }

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
}
