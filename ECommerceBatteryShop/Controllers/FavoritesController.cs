using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ECommerceBatteryShop.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly IFavoritesService _favoritesService;
        private const string CookieConsentCookieName = "COOKIE_CONSENT";
        private const string CookieConsentRejectedValue = "rejected";
        private const string FavoritesConsentMessage = "Çerezleri reddettiniz. Favoriler özelliğini kullanabilmek için çerezleri kabul etmelisiniz.";

        public FavoritesController(IFavoritesService favoritesService)
        {
            _favoritesService = favoritesService;
        }

        private bool IsCookieConsentRejected()
        {
            if (!Request.Cookies.TryGetValue(CookieConsentCookieName, out var consent))
            {
                return false;
            }

            return string.Equals(consent, CookieConsentRejectedValue, StringComparison.OrdinalIgnoreCase);
        }

        private IActionResult CookieConsentRequired(string message)
        {
            var payload = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["cookie-consent-required"] = message
            });

            Response.Headers["HX-Trigger"] = payload;
            return StatusCode(StatusCodes.Status409Conflict, new { message });
        }
        [HttpGet]
        public async Task<IActionResult> Index(CancellationToken ct)
        {
            FavoriteOwner owner;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst("sub")!.Value);
                owner = FavoriteOwner.FromUser(userId);
            }
            else
            {
                if (IsCookieConsentRejected())
                {
                    return View(new FavoriteViewModel
                    {
                        CookiesDisabled = true,
                        CookieMessage = FavoritesConsentMessage
                    });
                }

                var anonId = Request.Cookies["ANON_ID"];
                if (string.IsNullOrEmpty(anonId))
                {
                    return View(new FavoriteViewModel());
                }
                owner = FavoriteOwner.FromAnon(anonId);
            }
            var list = await _favoritesService.GetAsync(owner, createIfMissing: false, ct);
            var model = new FavoriteViewModel();
            if (list is not null)
            {
                model.Items = list.Items.Select(i => new FavoriteItemViewModel
                {
                    ProductId = i.ProductId,
                    Name = i.Product?.Name ?? string.Empty,
                    ImageUrl = i.Product?.ImageUrl,
                }).ToList();
            }
            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int productId, CancellationToken ct)
        {
            // 1) Owner çöz
            FavoriteOwner owner;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst("sub")!.Value);
                owner = FavoriteOwner.FromUser(userId);
            }
            else
            {
                if (IsCookieConsentRejected())
                {
                    return CookieConsentRequired(FavoritesConsentMessage);
                }

                var anonId = Request.Cookies["ANON_ID"];
                if (string.IsNullOrEmpty(anonId))
                {
                    anonId = Guid.NewGuid().ToString("N");
                    Response.Cookies.Append("ANON_ID", anonId, new CookieOptions
                    {
                        HttpOnly = true,
                        IsEssential = true,
                        SameSite = SameSiteMode.Lax,
                        Expires = DateTimeOffset.UtcNow.AddYears(1)
                    });
                }
                owner = FavoriteOwner.FromAnon(anonId);
            }

            // 2) Toggle et → sonuçtan toplamı öğren
            var result = await _favoritesService.ToggleAsync(owner, productId, ct);
            // result.Added : eklendi mi?
            // result.TotalCount : toggle sonrası toplam

            // 3) Son ürün de silindiyse tüm sayfayı yenile
            if (result.TotalCount == 0)
            {
                Response.Headers["HX-Refresh"] = "true";  // veya HX-Redirect
                return NoContent(); // 204, HTMX için yeterli
            }

            // 4) Aksi halde küçük bir JSON dön (badge vs. güncellemek için)
            return Ok(new { added = result.Added, total = result.TotalCount });
        }

    }
}
