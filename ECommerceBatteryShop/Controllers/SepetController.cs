using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Services;
using ECommerceBatteryShop.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using System.Text.Json;
using System.Security.Claims;

namespace ECommerceBatteryShop.Controllers
{
    public class SepetController : Controller
    {
        private readonly ICartRepository _repo;
        private readonly ICartService _cartService;
        private readonly ICurrencyService _currencyService;
        private readonly IAddressRepository _addressRepository;
        private const string CookieConsentCookieName = "COOKIE_CONSENT";
        private const string CookieConsentRejectedValue = "rejected";
        private const string CartConsentMessage = "Çerezleri reddettiniz. Sepet özelliğini kullanabilmek için çerezleri kabul etmelisiniz.";
        private const string GuestInfoCookie = "GUEST_INFO";
        public SepetController(ICartRepository repo, ICartService cartService, ICurrencyService currencyService, IAddressRepository addressRepository)
        {
            _repo = repo;
            _cartService = cartService;
            _currencyService = currencyService;
            _addressRepository = addressRepository;
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

        private GuestCheckoutViewModel? ReadGuestInfo()
        {
            try
            {
                if (Request.Cookies.TryGetValue(GuestInfoCookie, out var json) && !string.IsNullOrWhiteSpace(json))
                {
                    var guest = JsonSerializer.Deserialize<GuestCheckoutViewModel>(json);
                    return guest;
                }
            }
            catch
            {
                // ignore malformed cookie
            }
            return null;
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
            var rate = await _currencyService.GetCachedUsdTryAsync();
            decimal fx = rate ?? 41.5m;
            var cart = await _cartService.GetAsync(owner, createIfMissing: false, ct);
            var model = new CartViewModel();
            if (cart is not null)
            {
                model.Items = cart.Items.Select(i => new CartItemViewModel
                {
                    ProductId = i.ProductId,
                    Name = i.Product?.Name ?? string.Empty,
                    ImageUrl = i.Product?.ImageUrl,
                    UnitPrice = i.UnitPrice*1.2m*fx,
                    Quantity = i.Quantity
                }).ToList();
            }

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> Siparis()
        {
            CartOwner owner;
            var isAuthenticated = User.Identity?.IsAuthenticated == true;
            if (isAuthenticated)
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
            var rate = await _currencyService.GetCachedUsdTryAsync();
            decimal cartTotalPrice = await _cartService.CartTotalPriceAsync(owner);
            var subTotal = cartTotalPrice * (rate ?? 41.5m); // 1.2 = KDV

            IReadOnlyList<AddressViewModel> addresses = Array.Empty<AddressViewModel>();
            if (isAuthenticated)
            {
                var userId = int.Parse(User.FindFirst("sub")!.Value);
                var addressEntities = await _addressRepository.GetByUserAsync(userId, HttpContext.RequestAborted);
                addresses = addressEntities.Select(MapAddress).ToList();
            }

            // build brief cart items for the checkout page
            var cart = await _cartService.GetAsync(owner, createIfMissing: false, HttpContext.RequestAborted);
            var cartItems = cart?.Items.Select(i => new CartItemViewModel
            {
                ProductId = i.ProductId,
                Name = i.Product?.Name ?? string.Empty,
                ImageUrl = i.Product?.ImageUrl,
                UnitPrice = i.UnitPrice,
                Quantity = i.Quantity
            }).ToList() ?? new List<CartItemViewModel>();

            var guest = isAuthenticated ? null : ReadGuestInfo();

            var model = new CheckoutPageViewModel
            {
                SubTotal = subTotal,
                Addresses = addresses,
                IsGuest = !isAuthenticated,
                Guest = guest,
                CartItems = cartItems
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> MesafeliSatis(int? addressId, decimal? shipping, CancellationToken ct)
        {
            const decimal DefaultFx = 41.3m;
            const decimal KdvRate = 0.20m;
            const decimal DefaultShippingFee = 150m;

            var orderItems = new List<OrderItem>();

            CartOwner? owner = null;
            Cart? cart = null;

            if (User.Identity?.IsAuthenticated == true)
            {
                var userClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
                if (userClaim != null && int.TryParse(userClaim.Value, out var userId))
                {
                    owner = CartOwner.FromUser(userId);
                }
            }
            else if (!IsCookieConsentRejected())
            {
                var anonId = Request.Cookies["ANON_ID"];
                if (!string.IsNullOrWhiteSpace(anonId))
                {
                    owner = CartOwner.FromAnon(anonId);
                }
            }

            if (owner is CartOwner resolvedOwner)
            {
                cart = await _cartService.GetAsync(resolvedOwner, createIfMissing: true, ct);
            }

            var rate = await _currencyService.GetCachedUsdTryAsync(ct) ?? DefaultFx;

            if (cart is not null)
            {
                foreach (var item in cart.Items)
                {
                    var description = item.Product?.Name ?? $"Ürün #{item.ProductId}";
                    var unitPriceTry = decimal.Round(item.UnitPrice * (1 + KdvRate) * rate, 2, MidpointRounding.AwayFromZero);

                    orderItems.Add(new OrderItem
                    {
                        Quantity = item.Quantity,
                        UnitPrice = unitPriceTry
                    });
                }
            }

            if (orderItems.Count == 0)
            {
                orderItems.Add(new OrderItem
                {
                    Quantity = 1,
                    UnitPrice = 0m
                });
            }

            Address? selectedAddress = null;
            var buyerEmail = User.FindFirst(ClaimTypes.Email)?.Value;

            GuestCheckoutViewModel? guest = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userClaim = User.FindFirst("sub") ?? User.FindFirst(ClaimTypes.NameIdentifier);
                if (userClaim != null && int.TryParse(userClaim.Value, out var userId))
                {
                    if (addressId.HasValue)
                    {
                        selectedAddress = await _addressRepository.GetByIdAsync(userId, addressId.Value, ct);
                    }

                    if (selectedAddress is null)
                    {
                        var addresses = await _addressRepository.GetByUserAsync(userId, ct);
                        selectedAddress = addresses.FirstOrDefault(a => a.IsDefault) ?? addresses.FirstOrDefault();
                    }
                }
            }
            else
            {
                guest = ReadGuestInfo();
            }

            string buyerName;
            string buyerPhone;
            string buyerAddress;

            if (selectedAddress is not null)
            {
                buyerName = $"{selectedAddress.Name} {selectedAddress.Surname}".Trim();
                buyerPhone = selectedAddress.PhoneNumber ?? string.Empty;
                var addressParts = new[]
                {
                    selectedAddress.FullAddress,
                    selectedAddress.Neighbourhood,
                    string.Join('/', new[] { selectedAddress.State, selectedAddress.City }.Where(s => !string.IsNullOrWhiteSpace(s)))
                };
                buyerAddress = string.Join(" ", addressParts.Where(part => !string.IsNullOrWhiteSpace(part)));
            }
            else if (guest is not null)
            {
                buyerName = $"{guest.Name} {guest.Surname}".Trim();
                buyerPhone = guest.Phone ?? string.Empty;
                var addressParts = new[]
                {
                    guest.FullAddress,
                    guest.Neighbourhood,
                    string.Join('/', new[] { guest.State, guest.City }.Where(s => !string.IsNullOrWhiteSpace(s)))
                };
                buyerAddress = string.Join(" ", addressParts.Where(part => !string.IsNullOrWhiteSpace(part)));
                if (string.IsNullOrWhiteSpace(buyerEmail))
                {
                    buyerEmail = guest.Email;
                }
            }
            else
            {
                buyerName = "Belirtilmedi";
                buyerPhone = "+90 000 000 00 00";
                buyerAddress = "Belirtilmedi";
            }

            if (string.IsNullOrWhiteSpace(buyerName)) buyerName = "Belirtilmedi";
            if (string.IsNullOrWhiteSpace(buyerPhone)) buyerPhone = "+90 000 000 00 00";
            if (string.IsNullOrWhiteSpace(buyerAddress)) buyerAddress = "Belirtilmedi";
            buyerEmail = string.IsNullOrWhiteSpace(buyerEmail) ? "info@dayilyenerji.com" : buyerEmail;

            var shippingFee = orderItems.Any(i => i.UnitPrice > 0m)
                ? (shipping ?? DefaultShippingFee)
                : 0m;

            var model = new ContractViewModel
            {
                BuyerName = buyerName,
                BuyerAddress = buyerAddress,
                BuyerPhone = buyerPhone,
                BuyerEmail = buyerEmail!,
                OrdererName = buyerName,
                OrdererAddress = buyerAddress,
                OrdererPhone = buyerPhone,
                OrdererEmail = buyerEmail!,
                Items = orderItems,
                ShippingFee = shippingFee,
                InvoiceTitle = buyerName,
                InvoiceTax = "00000000000",
                InvoiceAddress = buyerAddress,
                InvoicePhone = buyerPhone,
                InvoiceEmail = buyerEmail!,
                ReturnPath = Url.Action("Iade", "Ev") ?? "/Ev/Iade",
                OrderDate = DateTime.Now
            };

            return View("~/Views/Ev/MesafeliSatis.cshtml", model);
        }

        private static AddressViewModel MapAddress(Address address)
        {
            return new AddressViewModel
            {
                Id = address.Id,
                UserId = address.UserId,
                Title = address.Title,
                Name = address.Name,
                Surname = address.Surname,
                PhoneNumber = address.PhoneNumber,
                FullAddress = address.FullAddress,
                City = address.City,
                State = address.State,
                Neighbourhood = address.Neighbourhood,
                IsDefault = address.IsDefault
            };
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
