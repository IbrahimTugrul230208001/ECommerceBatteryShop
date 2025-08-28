using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ECommerceBatteryShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductRepository _repo;
        private readonly ICurrencyService _currency;

        public HomeController(IProductRepository repo, ICurrencyService currency, ILogger<HomeController> log)
        { _repo = repo; _currency = currency; _logger = log; }

        public async Task<IActionResult> Index(CancellationToken ct)
        {
            const decimal KdvRate = 0.20m;

            var products = await _repo.GetMainPageProductsAsync(8, ct);
            var rate = await _currency.GetCachedUsdTryAsync(ct);
            if (rate is null)
            {
                TempData["FxNotice"] = "TRY conversion unavailable; showing USD.";
                _logger.LogWarning("USD?TRY unavailable; using USD display.");
            }

            var fx = rate ?? 1m;
            var vm = products.Select(p => new ProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = _currency.ConvertUsdToTry(p.Price, fx) * (1 + KdvRate),                Rating = p.Rating,
                ImageUrl = p.ImageUrl
            }).ToList();

            return View(vm); // View is strongly-typed to IEnumerable<ProductViewModel>
        }

        public IActionResult Cart()
        {
            // Placeholder for cart functionality
            return View();
        }
    }
}
