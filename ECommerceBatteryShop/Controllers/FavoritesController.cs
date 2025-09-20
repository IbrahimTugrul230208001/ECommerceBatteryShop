using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers
{
    public class FavoritesController : Controller
    {
        private readonly IFavoritesService _favoritesService;

        public FavoritesController(IFavoritesService favoritesService)
        {
            _favoritesService = favoritesService;
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
                var anonId = Request.Cookies["ANON_ID"];
                if (string.IsNullOrEmpty(anonId))
                {
                    return View(new CartViewModel());
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
            return Ok();
        }

    }
}
