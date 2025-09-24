using System.Security.Cryptography;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;

namespace ECommerceBatteryShop.Security;

public interface ICspNonceService
{
    string ScriptNonce { get; }
    string StyleNonce { get; }
}

public sealed class CspNonceService(IHttpContextAccessor httpContextAccessor) : ICspNonceService
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    public string ScriptNonce => GetNonce(CspKeys.Script);

    public string StyleNonce => GetNonce(CspKeys.Style);

    private string GetNonce(object key)
    {
        var context = _httpContextAccessor.HttpContext
            ?? throw new InvalidOperationException("No active HttpContext to resolve CSP nonces.");

        if (context.Items[key] is string nonce)
        {
            return nonce;
        }

        throw new InvalidOperationException(
            "CSP nonces are not available. Ensure UseContentSecurityPolicy() runs before rendering views.");
    }
}

public static class CspApplicationBuilderExtensions
{
    public static IApplicationBuilder UseContentSecurityPolicy(this IApplicationBuilder app)
    {
        return app.Use(async (context, next) =>
        {
            var scriptNonce = GenerateNonce();
            var styleNonce = GenerateNonce();

            context.Items[CspKeys.Script] = scriptNonce;
            context.Items[CspKeys.Style] = styleNonce;

            context.Response.OnStarting(() =>
            {
                var policies = new[]
                {
                    "default-src 'self'",
                    $"script-src 'self' 'nonce-{scriptNonce}' 'unsafe-eval' https://cdn.jsdelivr.net https://unpkg.com https://accounts.google.com https://apis.google.com",
                    $"style-src 'self' 'nonce-{styleNonce}' 'unsafe-inline' https://fonts.googleapis.com",
                    "img-src 'self' data: https://cdn.jsdelivr.net https://images.unsplash.com https://lh3.googleusercontent.com https://*.googleusercontent.com",
                    "font-src 'self' https://fonts.gstatic.com",
                    "connect-src 'self'",
                    "frame-src 'self' https://accounts.google.com",
                    "form-action 'self' https://accounts.google.com"
                };

                context.Response.Headers["Content-Security-Policy"] = string.Join("; ", policies);
                context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
                context.Response.Headers["X-Content-Type-Options"] = "nosniff";
                context.Response.Headers["X-Frame-Options"] = "DENY";

                return Task.CompletedTask;
            });

            await next();
        });
    }

    private static string GenerateNonce()
    {
        Span<byte> buffer = stackalloc byte[16];
        RandomNumberGenerator.Fill(buffer);
        return Convert.ToBase64String(buffer);
    }
}

internal static class CspKeys
{
    public static readonly object Script = new();
    public static readonly object Style = new();
}
