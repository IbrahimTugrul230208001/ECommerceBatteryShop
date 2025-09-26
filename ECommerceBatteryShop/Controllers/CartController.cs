
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBatteryShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartService _cartService;
        private readonly ICurrencyService _currencyService;
        private readonly BatteryShopContext _db;
        private const string CookieConsentCookieName = "COOKIE_CONSENT";
        private const string CookieConsentRejectedValue = "rejected";
        private const string CartConsentMessage = "Çerezleri reddettiniz. Sepet özelliğini kullanabilmek için çerezleri kabul etmelisiniz.";
        public CartController(ICartService cartService, ICurrencyService currencyService, BatteryShopContext db)
        {
            _cartService = cartService;
            _currencyService = currencyService;
            _db = db;
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

        public async Task<IActionResult> Index(CancellationToken ct)
        {
            CartOwner owner;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst("sub")!.Value);
                owner = CartOwner.FromUser(userId);
            }
            else
            {
                if (IsCookieConsentRejected())
                {
                    return View(new CartViewModel
                    {
                        CookiesDisabled = true,
                        CookieMessage = CartConsentMessage
                    });
                }

                var anonId = Request.Cookies["ANON_ID"];
                if (string.IsNullOrEmpty(anonId))
                {
                    return View(new CartViewModel());
                }
                owner = CartOwner.FromAnon(anonId);
            }

            var cart = await _cartService.GetAsync(owner, createIfMissing: false, ct);
            var model = new CartViewModel();
            if (cart is not null)
            {
                model.Items = cart.Items.Select(i => new CartItemViewModel
                {
                    ProductId = i.ProductId,
                    Name = i.Product?.Name ?? string.Empty,
                    ImageUrl = i.Product?.ImageUrl,
                    UnitPrice = i.UnitPrice*1.2m*41m,
                    Quantity = i.Quantity
                }).ToList();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Checkout(CancellationToken ct)
        {
            CartOwner owner;
            int? userId = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                userId = int.Parse(User.FindFirst("sub")!.Value);
                owner = CartOwner.FromUser(userId.Value);
            }
            else
            {
                if (IsCookieConsentRejected())
                {
                    return CookieConsentRequired(CartConsentMessage);
                }

                var anonId = Request.Cookies["ANON_ID"];
                if (string.IsNullOrEmpty(anonId))
                {
                    anonId = Guid.NewGuid().ToString();
                    Response.Cookies.Append("ANON_ID", anonId, new CookieOptions
                    {
                        HttpOnly = true,
                        IsEssential = true,
                        Expires = DateTimeOffset.UtcNow.AddMonths(3)
                    });
                }
                owner = CartOwner.FromAnon(anonId);
            }

            var rate = await _currencyService.GetCachedUsdTryAsync();
            var cartTotalPrice = await _cartService.CartTotalPriceAsync(owner);
            var subTotal = cartTotalPrice * (rate ?? 41.3m); // 1.2 = KDV

            var addressViewModels = new List<AddressViewModel>();
            if (userId.HasValue)
            {
                var addresses = await _db.Addresses
                    .AsNoTracking()
                    .Where(a => a.UserId == userId.Value)
                    .ToListAsync(ct);

                addressViewModels = addresses
                    .OrderByDescending(a => a.IsDefault)
                    .ThenBy(a => a.Id)
                    .Select(AddressViewModel.FromEntity)
                    .ToList();
            }

            var model = new CheckoutViewModel
            {
                Subtotal = subTotal,
                AddressList = new AddressListViewModel
                {
                    ContainerId = "address-list",
                    Addresses = addressViewModels
                }
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productId, int quantity, CancellationToken ct = default)
        {
            // resolve owner: account vs guest
            CartOwner owner;
            if (User.Identity?.IsAuthenticated == true)
            {
                // adapt this to however you store user id in claims
                var userId = int.Parse(User.FindFirst("sub")!.Value);
                owner = CartOwner.FromUser(userId);
            }
            else
            {
                if (IsCookieConsentRejected())
                {
                    return CookieConsentRequired(CartConsentMessage);
                }

                var anonId = Request.Cookies["ANON_ID"];
                if (string.IsNullOrEmpty(anonId))
                {
                    anonId = Guid.NewGuid().ToString();
                    Response.Cookies.Append("ANON_ID", anonId, new CookieOptions
                    {
                        HttpOnly = true,
                        IsEssential = true,
                        Expires = DateTimeOffset.UtcNow.AddMonths(3)
                    });
                }
                owner = CartOwner.FromAnon(anonId);
            }

            var count = await _cartService.AddAsync(owner, productId, quantity, ct);

            // returns updated count as partial view (HTMX/JS can swap it in header)
            return PartialView("_CartCount", count);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetQuantity(int productId, int quantity, CancellationToken ct = default)
        {
            CartOwner owner;
            if (User.Identity?.IsAuthenticated == true)
            {
                // adapt this to however you store user id in claims
                var userId = int.Parse(User.FindFirst("sub")!.Value);
                owner = CartOwner.FromUser(userId);
            }
            else
            {
                if (IsCookieConsentRejected())
                {
                    return CookieConsentRequired(CartConsentMessage);
                }

                var anonId = Request.Cookies["ANON_ID"];
                if (string.IsNullOrEmpty(anonId))
                {
                    anonId = Guid.NewGuid().ToString();
                    Response.Cookies.Append("ANON_ID", anonId, new CookieOptions
                    {
                        HttpOnly = true,
                        IsEssential = true,
                        Expires = DateTimeOffset.UtcNow.AddMonths(3)
                    });
                }
                owner = CartOwner.FromAnon(anonId);
            }
            var count = await _cartService.ChangeQuantityAsync(owner, productId, quantity, ct);

            return PartialView("_CartCount", count);
        }

        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> Delete(int productId, CancellationToken ct = default)
        {
            CartOwner owner;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst("sub")!.Value);
                owner = CartOwner.FromUser(userId);
            }
            else
            {
                if (IsCookieConsentRejected())
                {
                    return CookieConsentRequired(CartConsentMessage);
                }

                var anonId = Request.Cookies["ANON_ID"];
                if (string.IsNullOrEmpty(anonId))
                {
                    return PartialView("_CartCount", 0);
                }
                owner = CartOwner.FromAnon(anonId);
            }

            var count = await _cartService.RemoveAsync(owner, productId, ct);

            return PartialView("_CartCount", count);
        }
        [ValidateAntiForgeryToken]
        [HttpPost]
        public async Task<IActionResult> DeleteAll(CancellationToken ct = default)
        {
            CartOwner owner;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst("sub")!.Value);
                owner = CartOwner.FromUser(userId);
            }
            else
            {
                if (IsCookieConsentRejected())
                {
                    return CookieConsentRequired(CartConsentMessage);
                }

                var anonId = Request.Cookies["ANON_ID"];
                if (string.IsNullOrEmpty(anonId))
                {
                    return PartialView("_CartCount", 0);
                }
                owner = CartOwner.FromAnon(anonId);
            }

            var count = await _cartService.RemoveAllAsync(owner, ct);

            return PartialView("_CartCount", count);
        }
    }
}
