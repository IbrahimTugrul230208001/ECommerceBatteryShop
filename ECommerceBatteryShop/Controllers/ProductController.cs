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
        private readonly ILogger<ProductController> _log;

        public ProductController(IProductRepository repo, ICurrencyService currency, ILogger<ProductController> log)
        { _repo = repo; _currency = currency; _log = log; }

        public async Task<IActionResult> Products(CancellationToken ct)
        {
            var products = await _repo.GetMainPageProductsAsync(21, ct);

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
                Price = _currency.ConvertUsdToTry(p.Price /*USD*/, fx),
                Rating = p.Rating,
                ImageUrl = p.ImageUrl
            }).ToList();

            return View(vm);
        }
    }

}
