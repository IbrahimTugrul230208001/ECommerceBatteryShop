
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
    public class CartController : Controller
    {
        private readonly ICartRepository _repo;
        private readonly ICartService _cartService;
        private readonly ICurrencyService _currencyService;
        private readonly IAddressRepository _addressRepository;
        private readonly IIyzicoPaymentService _iyzicoPaymentService;
        private const string CookieConsentCookieName = "COOKIE_CONSENT";
        private const string CookieConsentRejectedValue = "rejected";
        private const string CartConsentMessage = "Çerezleri reddettiniz. Sepet özelliğini kullanabilmek için çerezleri kabul etmelisiniz.";
        public CartController(ICartRepository repo, ICartService cartService, ICurrencyService currencyService, IAddressRepository addressRepository, IIyzicoPaymentService iyzicoPaymentService)
        {
            _repo = repo;
            _cartService = cartService;
            _currencyService = currencyService;
            _addressRepository = addressRepository;
            _iyzicoPaymentService = iyzicoPaymentService;
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
        public async Task<IActionResult> Checkout()
        {
            var cancellationToken = HttpContext.RequestAborted;

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
            var cart = await _cartService.GetAsync(owner, createIfMissing: true, cancellationToken);
            var fxRate = await _currencyService.GetCachedUsdTryAsync(cancellationToken) ?? 41.3m;

            decimal subTotal = 0m;
            foreach (var item in cart.Items)
            {
                var lineTotal = decimal.Round(item.UnitPrice * item.Quantity * 1.2m * fxRate, 2, MidpointRounding.AwayFromZero);
                subTotal += lineTotal;
            }
            subTotal = decimal.Round(subTotal, 2, MidpointRounding.AwayFromZero);

            var buyerEmail = User.FindFirst(ClaimTypes.Email)?.Value;

            IReadOnlyList<AddressViewModel> addresses = Array.Empty<AddressViewModel>();
            AddressViewModel? primaryAddress = null;
            if (User.Identity?.IsAuthenticated == true)
            {
                var userId = int.Parse(User.FindFirst("sub")!.Value);
                var addressEntities = await _addressRepository.GetByUserAsync(userId, cancellationToken);
                addresses = addressEntities.Select(MapAddress).ToList();
                primaryAddress = addresses.FirstOrDefault(a => a.IsDefault) ?? addresses.FirstOrDefault();
            }

            var model = new CheckoutPageViewModel
            {
                SubTotal = subTotal,
                Addresses = addresses
            };

            var addressLine = primaryAddress is null
                ? string.Empty
                : string.Join(" ", new[]
                {
                    primaryAddress.FullAddress,
                    primaryAddress.Neighbourhood,
                    primaryAddress.State,
                    primaryAddress.City
                }.Where(part => !string.IsNullOrWhiteSpace(part)));

            var buyerInfo = new IyzicoBuyerInfo
            {
                Id = owner.IsUser ? owner.UserId?.ToString() : owner.AnonId,
                FirstName = primaryAddress?.Name,
                LastName = primaryAddress?.Surname,
                Email = buyerEmail,
                PhoneNumber = primaryAddress?.PhoneNumber,
                IdentityNumber = "11111111111",
                AddressLine = addressLine,
                City = primaryAddress?.City,
                Country = "Turkey",
                ZipCode = primaryAddress?.State
            };

            var callbackUrl = Url.Action(
                action: nameof(PaymentCallback),
                controller: "Cart",
                values: null,
                protocol: Request.Scheme);

            var checkoutContext = new IyzicoCheckoutContext
            {
                Cart = cart,
                ItemsTotal = subTotal,
                ShippingCost = model.ShippingCost,
                FxRate = fxRate,
                Buyer = buyerInfo,
                BuyerIpAddress = HttpContext.Connection.RemoteIpAddress?.ToString(),
                CallbackUrl = callbackUrl
            };

            model.IyzipayCheckoutFormContent = await _iyzicoPaymentService.InitializeCheckoutFormAsync(checkoutContext, cancellationToken);

            return View(model);
        }

        [HttpPost]
        [IgnoreAntiforgeryToken]
        public IActionResult PaymentCallback()
        {
            return Ok();
        }

        [HttpGet]
        public async Task<IActionResult> DistantSelling(int? addressId, decimal? shipping, CancellationToken ct)
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

            string buyerName = selectedAddress is not null
                ? $"{selectedAddress.Name} {selectedAddress.Surname}".Trim()
                : "Belirtilmedi";
            if (string.IsNullOrWhiteSpace(buyerName))
            {
                buyerName = "Belirtilmedi";
            }

            string buyerPhone = selectedAddress?.PhoneNumber ?? string.Empty;
            if (string.IsNullOrWhiteSpace(buyerPhone))
            {
                buyerPhone = "+90 000 000 00 00";
            }

            var addressParts = selectedAddress is null
                ? Array.Empty<string>()
                : new[]
                {
                    selectedAddress.FullAddress,
                    selectedAddress.Neighbourhood,
                    string.Join('/', new[] { selectedAddress.State, selectedAddress.City }.Where(s => !string.IsNullOrWhiteSpace(s)))
                };

            var buyerAddress = addressParts.Length == 0
                ? "Belirtilmedi"
                : string.Join(" ", addressParts.Where(part => !string.IsNullOrWhiteSpace(part)));

            buyerEmail = string.IsNullOrWhiteSpace(buyerEmail) ? "info@dayilyenerji.com" : buyerEmail;

            var shippingFee = orderItems.Any(i => i.UnitPrice > 0m)
                ? (shipping ?? DefaultShippingFee)
                : 0m;

            var model = new ContractViewModel
            {
                BuyerName = buyerName,
                BuyerAddress = buyerAddress,
                BuyerPhone = buyerPhone,
                BuyerEmail = buyerEmail,
                OrdererName = buyerName,
                OrdererAddress = buyerAddress,
                OrdererPhone = buyerPhone,
                OrdererEmail = buyerEmail,
                Items = orderItems,
                ShippingFee = shippingFee,
                InvoiceTitle = buyerName,
                InvoiceTax = "00000000000",
                InvoiceAddress = buyerAddress,
                InvoicePhone = buyerPhone,
                InvoiceEmail = buyerEmail,
                ReturnPath = Url.Action("Refund", "Home") ?? "/Home/Refund",
                OrderDate = DateTime.Now
            };

            return View("~/Views/Home/DistantSelling.cshtml", model);
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
