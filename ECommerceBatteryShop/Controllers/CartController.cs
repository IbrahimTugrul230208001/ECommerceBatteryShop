
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Mvc;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.Controllers
{
    public class CartController : Controller
    {
        private readonly ICartRepository _repo;
        private readonly ICartService _cartService;
        public CartController(ICartRepository repo, ICartService cartService)
        {
            _repo = repo;
            _cartService = cartService;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult Checkout()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Add(int productId, int quantity = 1, CancellationToken ct = default)
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

    }
}
