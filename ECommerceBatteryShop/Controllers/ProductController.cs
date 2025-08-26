using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers
{
  
    public class ProductController : Controller
    {
        private readonly IProductRepository _repo;
        private readonly ICurrencyService _currency;
        private readonly ILogger<ProductController> _logger;

        public ProductController(IProductRepository repo, ICurrencyService currency, ILogger<ProductController> logger)
        {
            _repo = repo; _currency = currency; _logger = logger;
        }

        public async Task<IActionResult> Products(decimal? minPrice, decimal? maxPrice, float? minRating, CancellationToken ct)
        {
            var products = await _repo.GetMainPageProductsAsync(21, ct);

            var rate = await _currency.TryGetUsdTryAsync(ct);
            if (rate is null)
            {
                TempData["FxNotice"] = "TRY conversion temporarily unavailable; showing USD.";
                _logger.LogWarning("USD→TRY unavailable; using USD display.");
            }

            var usdTry = rate ?? 1m; // if null, keep USD
            var vm = products.Select(p => new ProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = _currency.ConvertUsdToTry(p.Price, usdTry), // pure math
                Rating = p.Rating,
                ImageUrl = p.ImageUrl
            }).ToList();

            return View(vm);
        }

    }
}
