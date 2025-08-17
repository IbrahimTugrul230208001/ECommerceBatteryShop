using System.Diagnostics;
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
