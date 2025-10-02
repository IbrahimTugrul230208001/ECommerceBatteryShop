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

namespace ECommerceBatteryShop.Controllers;

public class PaymentController : Controller
{
    private readonly ICartService _cartService;
    private readonly IyzicoOptions _iyzicoOptions;
    private readonly ILogger<PaymentController> _logger;

    public PaymentController(
        ICartService cartService,
        IOptions<IyzicoOptions> iyzicoOptions,
        ILogger<PaymentController> logger)
    {
        _cartService = cartService;
        _iyzicoOptions = iyzicoOptions.Value;
        _logger = logger;
    }

    public IActionResult Index()
    {
        return View();
    }

    public async Task<IActionResult> InsertOrder()
    {
        var userIdClaim = User.FindFirst("sub") ?? User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
        if (userIdClaim is null || !int.TryParse(userIdClaim.Value, out var userId))
        {
            return RedirectToAction("Index", "Cart");
        }

        var owner = CartOwner.FromUser(userId);

        var cart = await _cartService.GetAsync(owner);
        if (cart is null || !cart.Items.Any())
        {
            return RedirectToAction("Index", "Cart");
        }

        return View(cart);
    }

    [AllowAnonymous]
    [HttpPost]
    [IgnoreAntiforgeryToken]
    [Route("Payment/iyzicoCallback")]
    public async Task<IActionResult> IyzicoCallback(CancellationToken cancellationToken)
    {
        Request.EnableBuffering();
        string payload;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            payload = await reader.ReadToEndAsync(cancellationToken);
        }
        Request.Body.Position = 0;

        var signature = Request.Headers["iyziSignature"].FirstOrDefault()
            ?? Request.Headers["x-iyzi-signature"].FirstOrDefault();

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
