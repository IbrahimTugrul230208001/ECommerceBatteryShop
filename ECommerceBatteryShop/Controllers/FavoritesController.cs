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

        // POST /favorites/toggle/123
        [HttpPost("toggle/{productId:int}")]
        [Produces("application/json")]
        public async Task<IActionResult> Toggle(int productId, CancellationToken ct)
        {
            // owner resolution (user or anon)
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

            var result = await _favoritesService.ToggleAsync(owner, productId, ct);
            return Ok(new { added = result.Added, total = result.TotalCount });
        }
    }
}
