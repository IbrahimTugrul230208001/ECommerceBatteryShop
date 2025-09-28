using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ECommerceBatteryShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly BatteryShopContext _context;

        public AdminController(BatteryShopContext context)
        {
            _context = context;
        }

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
        public async Task<IActionResult> Stocks(string? search, CancellationToken cancellationToken)
        {
            var model = new AdminStockViewModel
            {
                SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim()
            };

            if (!string.IsNullOrWhiteSpace(model.SearchTerm))
            {
                model.Items = await LoadStockItemsAsync(model.SearchTerm, cancellationToken);
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Stocks(AdminStockViewModel model, CancellationToken cancellationToken)
        {
            if (model?.Items is null || model.Items.Count == 0)
            {
                ModelState.AddModelError(string.Empty, "Güncellenecek ürün bulunamadı.");
            }

            if (!ModelState.IsValid)
            {
                model ??= new AdminStockViewModel();
                if (!string.IsNullOrWhiteSpace(model.SearchTerm))
                {
                    model.Items = await LoadStockItemsAsync(model.SearchTerm, cancellationToken);
                }
                return View(model);
            }

            var productIds = model.Items.Select(i => i.ProductId).ToList();

            var existingProducts = await _context.Products
                .Where(p => productIds.Contains(p.Id))
                .Select(p => p.Id)
                .ToListAsync(cancellationToken);

            var inventories = await _context.Inventories
                .Where(i => productIds.Contains(i.ProductId))
                .ToDictionaryAsync(i => i.ProductId, cancellationToken);

            var now = DateTime.UtcNow;

            foreach (var item in model.Items)
            {
                if (!existingProducts.Contains(item.ProductId))
                {
                    continue;
                }

                if (inventories.TryGetValue(item.ProductId, out var inventory))
                {
                    inventory.Exists = item.InStock;
                    inventory.LastUpdated = now;
                }
                else
                {
                    _context.Inventories.Add(new Inventory
                    {
                        ProductId = item.ProductId,
                        Exists = item.InStock,
                        LastUpdated = now
                    });
                }
            }

            await _context.SaveChangesAsync(cancellationToken);

            TempData["StockUpdateSuccess"] = "Seçili ürünlerin stok durumları güncellendi.";

            return RedirectToAction(nameof(Stocks));
        }

        [HttpGet]
        public async Task<IActionResult> StockSearch(string? query, CancellationToken cancellationToken)
        {
            var searchTerm = string.IsNullOrWhiteSpace(query) ? null : query.Trim();
            var items = await LoadStockItemsAsync(searchTerm, cancellationToken);

            return Json(new { items });
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

        private async Task<IList<AdminStockItemViewModel>> LoadStockItemsAsync(string? searchTerm, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<AdminStockItemViewModel>();
            }

            searchTerm = searchTerm.Trim();

            var query = _context.Products.AsNoTracking();
            var pattern = $"%{searchTerm}%";

            if (int.TryParse(searchTerm, out var productId))
            {
                query = query.Where(p => p.Id == productId || EF.Functions.ILike(p.Name, pattern));
            }
            else
            {
                query = query.Where(p => EF.Functions.ILike(p.Name, pattern));
            }

            return await query
                .OrderBy(p => p.Name)
                .Take(25)
                .Select(p => new AdminStockItemViewModel
                {
                    ProductId = p.Id,
                    ProductName = p.Name,
                    InStock = p.Inventory != null && p.Inventory.Exists
                })
                .ToListAsync(cancellationToken);
        }
    }
}
