using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
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
            const int perSection = 8;

            // category ids from your DB
            const int LiIonId = 20;
            const int BmsId = 51;
            const int LfpId = 22;

            var rate = await _currency.GetCachedUsdTryAsync(ct);
            if (rate is null)
            {
                TempData["FxNotice"] = "TRY conversion unavailable; showing USD.";
                _logger.LogWarning("USD?TRY unavailable; using USD display.");
            }
            var fx = rate ?? 1m;

            // Ensure this includes ProductCategories (CategoryId is enough; Category.Include not required)

            ProductViewModel Map(Product p) => new()
            {
                Id = p.Id,
                Name = p.Name,
                Price = _currency.ConvertUsdToTry(p.Price, fx) * (1 + KdvRate),
                Rating = p.Rating,
                ImageUrl = p.ImageUrl ?? string.Empty,
                Description = p.Description ?? string.Empty
            };
            var plan = new[]
            {
    new { Title = "Lithium-ion Pil",          CatId = LiIonId },
    new { Title = "BMS - Pil Koruma Devresi", CatId = BmsId   },
    new { Title = "LiFePO4 - Silindirik Pil", CatId = LfpId   },
};

            var sections = new List<ProductSectionViewModel>();
            var used = new HashSet<int>();

            foreach (var def in plan)
            {
                var raw = await _repo.BringProductsByCategoryIdAsync(def.CatId, 1, perSection * 2); // kk buffer
                var ps = raw.Where(p => !used.Contains(p.Id)).Take(perSection).ToList();
                foreach (var p in ps) used.Add(p.Id);

                if (ps.Count > 0)
                    sections.Add(new ProductSectionViewModel
                    {
                        Title = def.Title,
                        AllLink = $"/Product?categoryId={def.CatId}",
                        Products = ps.Select(Map).ToList()
                    });
            }
            return View(sections);

        }

    }
}
