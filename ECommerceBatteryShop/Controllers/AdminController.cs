using ECommerceBatteryShop.DataAccess;
using ECommerceBatteryShop.DataAccess.Abstract;
using ECommerceBatteryShop.Domain.Entities;
using ECommerceBatteryShop.Models;
using ECommerceBatteryShop.Services;
using Iyzipay.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace ECommerceBatteryShop.Controllers
{
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private readonly BatteryShopContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly ICategoryRepository _categoryRepository;  
        private readonly IOrderRepository _orderRepository;
        private readonly ICurrencyService _currencyService;
        public AdminController(BatteryShopContext context, IWebHostEnvironment environment, ICategoryRepository categoryRepository, IOrderRepository orderRepository, ICurrencyService currencyService)
        {
            _context = context;
            _environment = environment;
            _categoryRepository = categoryRepository;
            _orderRepository = orderRepository;
            _currencyService = currencyService;
        }

        [HttpGet]
        public async Task<IActionResult> UrunPaneli(int? productId, string? search, CancellationToken cancellationToken)
        {
            if (TempData.ContainsKey("ProductEntrySuccess"))
            {
                ViewBag.ProductEntrySuccess = TempData["ProductEntrySuccess"];
            }
            var model = new AdminProductEntryViewModel
            {
                SearchTerm = string.IsNullOrWhiteSpace(search) ? null : search.Trim(),
                Categories = await LoadCategoryItemsAsync(null)
            };
           
            

            await PopulateEntryViewModelAsync(model, productId, cancellationToken);
            if (productId.HasValue)
            {
                var selectedProduct = _context.ProductCategories.Where(c => c.ProductId == productId).FirstOrDefault();
                model.CategoryId = selectedProduct.CategoryId;
            }
            return View(model);
        }
        [HttpGet]
        public IActionResult AnalitikPaneli()
        {
             return View();
        }
        [HttpGet]
        public async Task<IActionResult> SiparisPaneli()
        {
            var orders = await _orderRepository.GetOrdersAsync();
            var rate = await _currencyService.GetCachedUsdTryAsync();
            decimal fx = rate ?? 41.5m;

            OrderViewModel vm = new OrderViewModel
            {
                Orders = orders,
                Payments = orders.SelectMany(o => o.Payments).ToList(),
                Rate= fx
            };
            return View(vm);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteProduct(int id, CancellationToken cancellationToken = default)
        {
            var product = await _context.Products.Include(p => p.Inventory).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);
            var productCategory = await _context.ProductCategories.Where(pc => pc.ProductId == id).FirstOrDefaultAsync();
            if (product.Inventory != null)
            {
                _context.Inventories.Remove(product.Inventory);
            }
            _context.ProductCategories.Remove(productCategory);
            _context.Products.Remove(product);
            await _context.SaveChangesAsync(cancellationToken);
         
            if (!string.IsNullOrWhiteSpace(product.ImageUrl))
            {
                var imagesFolder = Path.Combine(_environment.WebRootPath ?? string.Empty, "img");
                var imagePath = Path.Combine(imagesFolder, product.ImageUrl);
                if (System.IO.File.Exists(imagePath))
                {
                    System.IO.File.Delete(imagePath);
                }
            }
            TempData["ProductEntrySuccess"] = "Ürün başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }
        [HttpPost]
        public async Task<IActionResult> UpdateStatus(int orderId, string newStatus, CancellationToken cancellationToken)
        {
            if (orderId == 0 || newStatus == null)
            {
                return NotFound("Sipariş bulunamadı.");
            }
            else
            {
                await _orderRepository.UpdateOrderStatusAsync(orderId, newStatus);
                TempData["OrderStatusSuccess"] = "Sipariş durumu başarıyla güncellendi.";
                return RedirectToAction("Orders", "Admin");
            }
        }
        [HttpPost]
        public async Task<IActionResult> DeleteCategory(int categoryId, CancellationToken cancellationToken)
        {
            var category = await _context.Categories.FirstOrDefaultAsync(c => c.Id == categoryId, cancellationToken);
            if (category is null)
            {
                TempData["CategoryError"] = "Silinecek kategori bulunamadı veya zaten silinmiş olabilir.";
                return RedirectToAction(nameof(Index));
            }
            var hasProducts = await _context.ProductCategories.AnyAsync(pc => pc.CategoryId == categoryId, cancellationToken);
            if (hasProducts)
            {
                TempData["CategoryError"] = "Bu kategoriye bağlı ürünler olduğu için silinemez.";
                return RedirectToAction(nameof(Index));
            }
            _context.Categories.Remove(category);
            await _context.SaveChangesAsync(cancellationToken);
            TempData["CategorySuccess"] = "Kategori başarıyla silindi.";
            return RedirectToAction(nameof(Index));
        }
        [HttpGet]
        public async Task<IActionResult> StokPaneli(string? search, CancellationToken cancellationToken)
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
                    inventory.Quantity = item.Quantity;
                    inventory.LastUpdated = now;
                }
                else
                {
                    _context.Inventories.Add(new Inventory
                    {
                        ProductId = item.ProductId,
                        Quantity = item.Quantity,
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
        public async Task<IActionResult> CreateProduct(AdminProductEntryViewModel model, CancellationToken cancellationToken)
        {
            model.SearchTerm = string.IsNullOrWhiteSpace(model.SearchTerm) ? null : model.SearchTerm.Trim();
            model.SearchResults = await LoadProductSelectionItemsAsync(model.SearchTerm, cancellationToken);
            // Ensure categories are available when returning the form
            model.Categories = await LoadCategoryItemsAsync(null);

            if (model.ProductId.HasValue)
            {
                var exists = await _context.Products.AnyAsync(p => p.Id == model.ProductId.Value, cancellationToken);
                if (!exists)
                {
                    ModelState.AddModelError(string.Empty, "Güncellenecek ürün bulunamadı veya silinmiş olabilir.");
                }
            }

            if (model.Image is not null)
            {
                if (string.IsNullOrWhiteSpace(model.Image.ContentType) || !model.Image.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
                {
                    ModelState.AddModelError(nameof(model.Image), "Lütfen geçerli bir görsel dosyası yükleyin.");
                }
            }

            if (model.Document is not null)
            {
                var ext = Path.GetExtension(model.Document.FileName);
                var isPdf = string.Equals(ext, ".pdf", StringComparison.OrdinalIgnoreCase) || string.Equals(model.Document.ContentType, "application/pdf", StringComparison.OrdinalIgnoreCase);
                if (!isPdf)
                {
                    ModelState.AddModelError(nameof(model.Document), "Lütfen PDF belge yükleyin (.pdf).");
                }
            }

            if (!ModelState.IsValid)
            {
                return View(nameof(UrunPaneli), model);
            }

            var isNew = !model.ProductId.HasValue;
            Product product;
            if (isNew)
            {
                product = new Product
                {
                    Rating = 0,
                    ExtraAmount = 0
                };
                _context.Products.Add(product);
            }
            else
            {
                product = await _context.Products.FirstAsync(p => p.Id == model.ProductId!.Value, cancellationToken);
            }
            
            product.Name = model.Name.Trim();
            product.Price = model.Price!.Value;
            product.Description = model.Description.Trim();

            var imagesFolder = Path.Combine(_environment.WebRootPath ?? string.Empty, "img");
            var documentsFolder = Path.Combine(_environment.WebRootPath ?? string.Empty, "doc");
            Directory.CreateDirectory(imagesFolder);
            Directory.CreateDirectory(documentsFolder);
            string? savedFileName = product.ImageUrl ?? model.ExistingImageUrl;
            string? savedDocumentFileName = product.DocumentUrl ?? model.ExistingDocumentUrl;

            // Handle document upload (PDF)
            if (model.Document is not null && model.Document.Length > 0)
            {
                var extension = Path.GetExtension(model.Document.FileName);
                if (!string.Equals(extension, ".pdf", StringComparison.OrdinalIgnoreCase))
                {
                    extension = ".pdf";
                }
                var newDocFileName = CreateDocumentFileName(product.Name, extension);
                var docPath = Path.Combine(documentsFolder, newDocFileName);
                await using (var stream = System.IO.File.Create(docPath))
                {
                    await model.Document.CopyToAsync(stream);
                }

                if (!string.IsNullOrWhiteSpace(savedDocumentFileName) && !string.Equals(savedDocumentFileName, newDocFileName, System.StringComparison.OrdinalIgnoreCase))
                {
                    var previousDocPath = Path.Combine(documentsFolder, savedDocumentFileName);
                    if (System.IO.File.Exists(previousDocPath))
                    {
                        System.IO.File.Delete(previousDocPath);
                    }
                }

                savedDocumentFileName = newDocFileName;
            }

            // Handle image upload
            if (model.Image is not null && model.Image.Length > 0)
            {
                var extension = Path.GetExtension(model.Image.FileName);
                if (string.IsNullOrWhiteSpace(extension))
                {
                    extension = model.Image.ContentType?.ToLowerInvariant() switch
                    {
                        "image/png" => ".png",
                        "image/webp" => ".webp",
                        _ => ".jpg"
                    };
                }

                var newFileName = CreateImageFileName(product.Name, extension);
                var filePath = Path.Combine(imagesFolder, newFileName);

                await using (var stream = System.IO.File.Create(filePath))
                {
                    await model.Image.CopyToAsync(stream);
                }

                if (!string.IsNullOrWhiteSpace(savedFileName) && !string.Equals(savedFileName, newFileName, System.StringComparison.OrdinalIgnoreCase))
                {
                    var previousPath = Path.Combine(imagesFolder, savedFileName);
                    if (System.IO.File.Exists(previousPath))
                    {
                        System.IO.File.Delete(previousPath);
                    }
                }

                savedFileName = newFileName;
            }

            if (!string.IsNullOrWhiteSpace(savedFileName))
            {
                product.ImageUrl = savedFileName;
            }
            if (!string.IsNullOrWhiteSpace(savedDocumentFileName))
            {
                product.DocumentUrl = savedDocumentFileName;
            }

            await _context.SaveChangesAsync(cancellationToken);
            await AssignCategoryAsync(product.Id, model.CategoryId, cancellationToken);
            TempData["ProductEntrySuccess"] = isNew
                ? "Yeni ürün başarıyla oluşturuldu."
                : "Ürün bilgileri başarıyla güncellendi.";

            return RedirectToAction(nameof(UrunPaneli), new { productId = product.Id, search = model.SearchTerm });
        }
        private async Task AssignCategoryAsync(int productId, int? categoryId, CancellationToken ct=default)
        {
            // Treat null/zero/invalid as remove all links
            if (!categoryId.HasValue || categoryId.Value <= 0 || !await _context.Categories.AnyAsync(c => c.Id == categoryId.Value, ct))
            {
                var toRemoveAll = _context.ProductCategories.Where(pc => pc.ProductId == productId);
                if (await toRemoveAll.AnyAsync(ct))
                {
                    _context.ProductCategories.RemoveRange(toRemoveAll);
                    await _context.SaveChangesAsync(ct);
                }
                return;
            }

            var links = await _context.ProductCategories
                .Where(pc => pc.ProductId == productId)
                .ToListAsync(ct);

            // If already linked to selected category, keep exactly one and remove others
            if (links.Any(pc => pc.CategoryId == categoryId.Value))
            {
                var extras = links.Where(pc => pc.CategoryId != categoryId.Value).ToList();
                if (extras.Count > 0)
                {
                    _context.ProductCategories.RemoveRange(extras);
                    await _context.SaveChangesAsync(ct);
                }
                return;
            }

            // Not linked yet (new product or changed): clear others and add
            if (links.Count > 0)
            {
                _context.ProductCategories.RemoveRange(links);
            }
            _context.ProductCategories.Add(new ProductCategory { ProductId = productId, CategoryId = categoryId.Value });
            await _context.SaveChangesAsync(ct);
        }
        private async Task<IList<AdminStockItemViewModel>> LoadStockItemsAsync(string? searchTerm, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(searchTerm))
            {
                return new List<AdminStockItemViewModel>();
            }

            searchTerm = searchTerm.Trim();

            var query = _context.Products.AsNoTracking();
            var pattern = $"/StokPaneli/%{searchTerm}%";

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
                    Quantity = p.Inventory != null && p.Inventory.Quantity >= 0 ? p.Inventory.Quantity : 0
                })
                .ToListAsync(cancellationToken);
        }

        private async Task PopulateEntryViewModelAsync(AdminProductEntryViewModel model, int? productId, CancellationToken cancellationToken)
        {
            model.SearchResults = await LoadProductSelectionItemsAsync(model.SearchTerm, cancellationToken);

            if (!productId.HasValue)
            {
                return;
            }

            var product = await _context.Products.AsNoTracking().FirstOrDefaultAsync(p => p.Id == productId.Value, cancellationToken);
            if (product is null)
            {
                ModelState.AddModelError(string.Empty, "Seçili ürün bulunamadı veya silinmiş olabilir.");
                return;
            }

            model.ProductId = product.Id;
            model.Name = product.Name;
            model.Price = product.Price;
            model.Description = product.Description ?? string.Empty;
            model.ExistingImageUrl = product.ImageUrl;

            if (model.SearchResults.All(r => r.Id != product.Id))
            {
                model.SearchResults.Insert(0, new AdminProductSelectionItemViewModel
                {
                    Id = product.Id,
                    Name = product.Name,
                    Price = product.Price,
                    ImageUrl = product.ImageUrl
                });
            }
        }

        private async Task<IList<AdminProductSelectionItemViewModel>> LoadProductSelectionItemsAsync(string? searchTerm, CancellationToken cancellationToken)
        {
            var query = _context.Products.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                var pattern = $"/UrunPaneli/%{searchTerm}%";

                if (int.TryParse(searchTerm, out var productId))
                {
                    query = query.Where(p => p.Id == productId || EF.Functions.ILike(p.Name, pattern));
                }
                else
                {
                    query = query.Where(p => EF.Functions.ILike(p.Name, pattern));
                }
            }

            return await query
                .OrderBy(p => p.Name)
                .Take(15)
                .Select(p => new AdminProductSelectionItemViewModel
                {
                    Id = p.Id,
                    Name = p.Name,
                    Price = p.Price,
                    ImageUrl = p.ImageUrl
                })
                .ToListAsync(cancellationToken);
        }

        private static string CreateImageFileName(string productName, string extension)
        {
            extension = string.IsNullOrWhiteSpace(extension) || !extension.StartsWith('.')
                ? ".jpg"
                : extension.ToLowerInvariant();

            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(productName.Where(c => !char.IsWhiteSpace(c) && !invalidChars.Contains(c)).ToArray());

            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "urun";
            }

            return sanitized + extension;
        }
        public static string CreateDocumentFileName(string productName, string extension)
        {
            extension = string.IsNullOrWhiteSpace(extension) || !extension.StartsWith('.')
                ? ".pdf"
                : extension.ToLowerInvariant();
            var invalidChars = Path.GetInvalidFileNameChars();
            var sanitized = new string(productName.Where(c => !char.IsWhiteSpace(c) && !invalidChars.Contains(c)).ToArray());
            if (string.IsNullOrWhiteSpace(sanitized))
            {
                sanitized = "urun";
            }
            return sanitized + extension;
        }
        private async Task<List<CategorySelectionViewModel>> LoadCategoryItemsAsync(int? selectedId = null)
        {
            var items = await _context.Categories
                .AsNoTracking()
                .OrderBy(c => c.Name).Where(c=>c.ParentCategoryId != null)
                .Select(c => new CategorySelectionViewModel
                {
                    CategoryId = c.Id,
                    CategoryName = c.Name,
                })
                .ToListAsync();

            return items;
        }

    }
}
