using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Mvc;
using ECommerceBatteryShop.Domain.Entities;

namespace ECommerceBatteryShop.Controllers
{
    public class ProductController : Controller
    {
        private readonly IProductRepository _repo;
        private readonly ICurrencyService _currency;
        private readonly ILogger<ProductController> _log;
        private readonly IFavoritesService _favorites;

        public ProductController(IProductRepository repo, ICurrencyService currency, ILogger<ProductController> log, IFavoritesService favorites)
        {
            _repo = repo; _currency = currency; _log = log; _favorites = favorites;

        }

        public async Task<IActionResult> Index(string? search, string? q, int? categoryId, CancellationToken ct)
        {

            var term = search ?? q;

            IReadOnlyList<Product> products;
            if (!string.IsNullOrWhiteSpace(term))
            {
                products = await _repo.ProductSearchResultAsync(term);
            }
            else if (categoryId.HasValue && categoryId > 0)
            {
                products = await _repo.BringProductsByCategoryIdAsync(categoryId.Value, ct: ct);
            }
            else
            {
                products = await _repo.GetMainPageProductsAsync(21, ct);
            }

            const decimal KdvRate = 0.20m;
            var favoriteIds = await LoadFavoriteIdsAsync(ct);

            var rate = await _currency.GetCachedUsdTryAsync(ct);
            if (rate is null)
            {
                TempData["FxNotice"] = "TRY conversion unavailable; showing USD.";
                _log.LogWarning("USD→TRY unavailable; using USD display.");
            }

            var fx = rate ?? 1m;
            var vm = products.Select(p => new ProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = _currency.ConvertUsdToTry(p.Price /*USD*/, fx) * (1 + KdvRate),
                Rating = p.Rating,
                ImageUrl = p.ImageUrl,
                IsFavorite = favoriteIds.Contains(p.Id)
            }).ToList();

            return View(vm);

            async Task<HashSet<int>> LoadFavoriteIdsAsync(CancellationToken token)
            {
                FavoriteOwner? owner = null;

                if (User.Identity?.IsAuthenticated == true)
                {
                    var sub = User.FindFirst("sub")?.Value;
                    if (int.TryParse(sub, out var userId))
                    {
                        owner = FavoriteOwner.FromUser(userId);
                    }
                }
                else
                {
                    var anonId = Request.Cookies["ANON_ID"];
                    if (!string.IsNullOrWhiteSpace(anonId))
                    {
                        owner = FavoriteOwner.FromAnon(anonId);
                    }
                }

                if (owner is null)
                {
                    return new HashSet<int>();
                }

                var list = await _favorites.GetAsync(owner, createIfMissing: false, token);

                return list is null
                    ? new HashSet<int>()
                    : new HashSet<int>(list.Items.Select(i => i.ProductId));
            }

        }
        [HttpGet] // optional
        public async Task<IActionResult> Details(int id, CancellationToken ct = default)
        {
            var product = await _repo.GetProductAsync(id, ct);
            if (product is null) return NotFound();

            const decimal KdvRate = 0.20m;
            var rate = await _currency.GetCachedUsdTryAsync(ct);
            var fx = rate ?? 1m;

            var vm = new ProductViewModel
            {
                Id = product.Id,
                Name = product.Name,
                Price = _currency.ConvertUsdToTry(product.Price, fx) * (1 + KdvRate),
                Rating = product.Rating,
                ImageUrl = product.ImageUrl,
                Description = product.Description
            };

            return View("Details", vm); // full view under _Layout
        }
        [HttpGet("/Product/Search")]
        public async Task<IActionResult> Search([FromQuery] string q, CancellationToken ct = default)
        {
            var names = await _repo.ProductSearchQueryResultAsync(q ?? string.Empty);
            return PartialView("_ProductPredictions", names);
        }

    }

}
