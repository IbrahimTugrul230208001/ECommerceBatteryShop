using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Models;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers
{
  
    public class ProductController : Controller
    {
        private readonly ILogger<ProductController> _logger;
        private readonly IProductRepository _repo;

        public ProductController(ILogger<ProductController> logger, IProductRepository repo)
        {
            _logger = logger;
            _repo = repo;
        }
        public async Task<IActionResult> Products(decimal? minPrice, decimal? maxPrice, float? minRating, CancellationToken ct)
        {
            var products = await _repo.GetMainPageProductsAsync(8, ct);
            var vm = products.Select(p => new ProductViewModel
            {
                Id = p.Id,
                Name = p.Name,
                Price = p.Price,
                Rating = p.Rating,
                ImageUrl = p.ImageUrl
            }).ToList();

            return View(vm); // View is strongly-typed to IEnumerable<ProductViewModel>
        }
    }
}
