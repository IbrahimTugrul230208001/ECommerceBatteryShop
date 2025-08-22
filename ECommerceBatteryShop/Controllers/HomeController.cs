using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Models;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace ECommerceBatteryShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IProductRepository _repo;

        public HomeController(ILogger<HomeController> logger, IProductRepository repo)
        {
            _logger = logger;
            _repo = repo;
        }

        public async Task<IActionResult> Index(CancellationToken ct)
        {
            var products = await _repo.GetMainPageProductsAsync(8, ct);
            var vm = products.Select(p => new ProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                Rating = p.Rating
            }).ToList();

            return View(vm); // View is strongly-typed to IEnumerable<ProductViewModel>
        }

        public async Task<IActionResult> Products(decimal? minPrice, decimal? maxPrice, float? minRating, CancellationToken ct)
        {
            var products = await _repo.GetMainPageProductsAsync(20, ct); // or apply filters later
            return View(products);
        }
        public IActionResult Privacy()
        {
            return View();
        }
        public IActionResult Cart()
        {
            // Placeholder for cart functionality
            return View();
        }
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
