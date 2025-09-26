using ECommerceBatteryShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ECommerceBatteryShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        [HttpGet]
        public IActionResult Index()
        {
            if (TempData.ContainsKey("ProductEntrySuccess"))
            {
                ViewBag.ProductEntrySuccess = TempData["ProductEntrySuccess"];
            }

            return View(new AdminProductEntryViewModel());
        }
        [HttpGet]
        public IActionResult Analytics()
        {
             return View();
        }
        [HttpGet]
        public IActionResult Orders()
        {
            return View();
        }
        [HttpGet]
        public IActionResult Stock()
        {
            return View();
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult CreateProduct(AdminProductEntryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View("Index", model);
            }

            TempData["ProductEntrySuccess"] = "Ürün taslağı başarıyla kaydedildi. Şimdi ürün detaylarını inceleyebilirsiniz.";
            return RedirectToAction(nameof(Index));
        }
    }
}
