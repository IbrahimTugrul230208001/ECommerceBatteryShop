using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using ECommerceBatteryShop.Models;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            var products = new List<ProductViewModel>
            {
                new() { Name = "AA Battery", Price = 2.99m, ImageUrl = "https://via.placeholder.com/150" },
                new() { Name = "AAA Battery", Price = 1.99m, ImageUrl = "https://via.placeholder.com/150" },
                new() { Name = "C Battery", Price = 3.99m, ImageUrl = "https://via.placeholder.com/150" },
                new() { Name = "D Battery", Price = 4.99m, ImageUrl = "https://via.placeholder.com/150" },
                new() { Name = "9V Battery", Price = 5.49m, ImageUrl = "https://via.placeholder.com/150" },
                new() { Name = "Button Cell", Price = 0.99m, ImageUrl = "https://via.placeholder.com/150" },
                new() { Name = "Rechargeable Pack", Price = 14.99m, ImageUrl = "https://via.placeholder.com/150" },
                new() { Name = "Lithium Battery", Price = 6.99m, ImageUrl = "https://via.placeholder.com/150" }
            };

            return View(products);
        }

        public IActionResult Products(decimal? minPrice, decimal? maxPrice, float? minRating)
        {
            var products = new List<Product>
            {
                new() { Id = 1, Name = "PowerMax 5000 Battery", Price = 299.90m, ImageUrl = "/img/battery.jpg", Rating = 4 },
                new() { Id = 2, Name = "EcoCharge AA Rechargeable Pack", Price = 79.50m, ImageUrl = "/img/battery.jpg", Rating = 5 },
                new() { Id = 3, Name = "UltraLife Car Battery 12V", Price = 850.00m, ImageUrl = "/img/battery.jpg", Rating = 3 },
                new() { Id = 4, Name = "MiniCell CR2032 Lithium", Price = 15.99m, ImageUrl = "/img/battery.jpg", Rating = 4 },
                new() { Id = 5, Name = "VoltCore 9V Pro Pack", Price = 129.00m, ImageUrl = "/img/battery.jpg", Rating = 5 },
                new() { Id = 6, Name = "SolarEdge Power Bank 20K", Price = 499.00m, ImageUrl = "/img/battery.jpg", Rating = 4 },
                new() { Id = 7, Name = "HeavyDuty Truck Battery 24V", Price = 1490.00m, ImageUrl = "/img/battery.jpg", Rating = 4 },
                new() { Id = 8, Name = "NanoCell AAA 24-Pack", Price = 59.90m, ImageUrl = "/img/battery.jpg", Rating = 3 }
            };

            if (minPrice.HasValue)
                products = products.Where(p => p.Price >= minPrice.Value).ToList();
            if (maxPrice.HasValue)
                products = products.Where(p => p.Price <= maxPrice.Value).ToList();
            if (minRating.HasValue)
                products = products.Where(p => p.Rating >= minRating.Value).ToList();

            var vm = new ProductListViewModel
            {
                Products = products,
                MinPrice = minPrice,
                MaxPrice = maxPrice,
                MinRating = minRating
            };

            return View(vm);
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
