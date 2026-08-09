using ECommerceBatteryShop.Infrastructure;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ECommerceBatteryShop.Controllers
{
    public class FavoriController : Controller
    {
        private readonly IFavoritesService _favoritesService;
        private readonly ICurrencyService _currency;
        private readonly IPricingService _pricing;
        private const string CookieConsentCookieName = "COOKIE_CONSENT";
        private const string CookieConsentRejectedValue = "rejected";
        private const string FavoritesConsentMessage = "Çerezleri reddettiniz. Favoriler özelliğini kullanabilmek için çerezleri kabul etmelisiniz.";

        public FavoriController(IFavoritesService favoritesService, ICurrencyService currency, IPricingService pricing)
        {
            _favoritesService = favoritesService;
            _currency = currency;
            _pricing = pricing;
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
            var userId = User.GetUserId();
            if (userId is not null)
            {
                owner = FavoriteOwner.FromUser(userId.Value);
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

                var anonId = AnonymousId.Read(Request);
                if (anonId is null)
                {
                    return View(new FavoriteViewModel());
                }
                owner = FavoriteOwner.FromAnon(anonId);
            }
            var list = await _favoritesService.GetAsync(owner, createIfMissing: false, ct);
            if (list is null || list.Items.Count == 0) return View(new FavoriteViewModel());

            var rate = await _currency.GetCachedUsdTryAsync(ct);
            var fx = rate ?? _currency.FallbackRate;

            var model = new FavoriteViewModel
            {
                Items = list.Items.Select(i =>
                {
                    var priced = i.Product is null
                        ? new PricedUnit(0m, null, 0m)
                        : _pricing.PriceUnit(i.Product, fx);

                    return new FavoriteItemViewModel
                    {
                        ProductId = i.ProductId,
                        UnitPrice = priced.Final,
                        OriginalPrice = priced.Original,
                        DiscountPercentage = priced.DiscountPercentage,
                        Name = i.Product?.Name ?? string.Empty,
                        ImageUrl = i.Product?.ImageUrl,
                        Slug = i.Product?.Slug,
                        StockQuantity = i.Product?.Inventory?.Quantity ?? 0
                    };
                }).ToList()
            };

            return View(model);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Toggle(int productId, CancellationToken ct)
        {
            // 1) Owner çöz
            FavoriteOwner owner;
            var userId = User.GetUserId();
            if (userId is not null)
            {
                owner = FavoriteOwner.FromUser(userId.Value);
            }
            else
            {
                if (IsCookieConsentRejected())
                {
                    return CookieConsentRequired(FavoritesConsentMessage);
                }

                owner = FavoriteOwner.FromAnon(AnonymousId.Ensure(HttpContext));
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
            return Ok();
        }

    }
}
