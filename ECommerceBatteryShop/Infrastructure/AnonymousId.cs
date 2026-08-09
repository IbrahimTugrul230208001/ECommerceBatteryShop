using System.Security.Claims;

namespace ECommerceBatteryShop.Infrastructure;

/// <summary>
/// Single source of truth for the anonymous (guest) identifier cookie and its policy.
/// Previously the read/create logic was copy-pasted across Sepet/Favori controllers with
/// inconsistent expiry (3 months vs 1 year) and id format.
/// </summary>
public static class AnonymousId
{
    public const string CookieName = "ANON_ID";
    private static readonly TimeSpan Lifetime = TimeSpan.FromDays(365);

    /// <summary>Returns the existing anonymous id, or null if the cookie is absent/empty.</summary>
    public static string? Read(HttpRequest request)
        => request.Cookies.TryGetValue(CookieName, out var value) && !string.IsNullOrWhiteSpace(value)
            ? value
            : null;

    /// <summary>Returns the existing anonymous id, creating and persisting a new one if needed.</summary>
    public static string Ensure(HttpContext context)
    {
        var existing = Read(context.Request);
        if (!string.IsNullOrWhiteSpace(existing))
        {
            return existing;
        }

        var id = Guid.NewGuid().ToString("N");
        context.Response.Cookies.Append(CookieName, id, new CookieOptions
        {
            HttpOnly = true,
            IsEssential = true,
            SameSite = SameSiteMode.Lax,
            Expires = DateTimeOffset.UtcNow.Add(Lifetime)
        });
        return id;
    }
}

public static class ClaimsPrincipalExtensions
{
    /// <summary>Reads the numeric user id from the "sub"/NameIdentifier claim, or null (e.g. guests, admin).</summary>
    public static int? GetUserId(this ClaimsPrincipal user)
    {
        var claim = user.FindFirst("sub") ?? user.FindFirst(ClaimTypes.NameIdentifier);
        return claim is not null && int.TryParse(claim.Value, out var id) ? id : null;
    }
}
